using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Security;

internal enum DeviceCredentialKind{GoogleClient,GoogleToken}
internal sealed record DeviceCredentialScope(int Version,string StoreId,string InstanceId,string BusinessId,string Mode);
internal sealed class DeviceCredentialFiles
{
    private readonly BusinessStore _store;
    private readonly DeviceCredentialScope _scope;
    private readonly string _root,_scopeJson;
    internal string DirectoryPath {get;}
    internal Action<string>? FailurePoint {get;set;}
    internal DeviceCredentialFiles(BusinessStore store):this(store,DefaultRoot()){}
    internal DeviceCredentialFiles(BusinessStore store,string root)
    {
        _store=store;_root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));string businessRoot=Path.GetDirectoryName(Path.GetFullPath(store.DatabasePath))!;
        if(Contains(_root,businessRoot)||Contains(businessRoot,_root))throw new ArgumentException("Keep device credential storage separate from the business directory.");
        _scope=store.ReadSnapshot(db=>new DeviceCredentialScope(1,store.StoreId,store.InstanceId,db.Read("SELECT Id FROM Business;",r=>r.GetString(0)).Single(),(string)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!));
        if(_scope.Mode is not("TEST" or "LIVE"))throw new InvalidDataException("The device credential mode is unsupported.");
        _scopeJson=JsonSerializer.Serialize(_scope);DirectoryPath=Path.Combine(_root,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_scopeJson))));
    }
    internal byte[]? Read(DeviceCredentialKind kind)
    {
        RequireCurrent();string path=FilePath(kind);RejectLinks(path);if(!File.Exists(path))return null;using var gate=Gate();
        using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(input.Length is <1 or >DeviceCredentialProtection.MaximumProtectedBytes)throw new InvalidDataException("The saved device credential file exceeds its supported size.");
        byte[] encrypted=new byte[checked((int)input.Length)];input.ReadExactly(encrypted);byte[] clear=DeviceCredentialProtection.Unprotect(encrypted,Context(kind));
        try{RequireCurrent();return clear;}catch{CryptographicOperations.ZeroMemory(clear);throw;}
    }
    internal void Write(DeviceCredentialKind kind,byte[] clear,Action<DbWork>? authorize=null)
    {
        RequireCurrent();string path=FilePath(kind);byte[] encrypted=DeviceCredentialProtection.Protect(clear,Context(kind));EnsureDirectory();using var gate=Gate();RejectLinks(path);
        string temporary=Path.Combine(DirectoryPath,Guid.NewGuid().ToString("N")+".partial");
        try
        {
            using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(encrypted);output.Flush(flushToDisk:true);}FailurePoint?.Invoke("Protected file flushed");
            _store.Transaction(db=>{RequireScope(db);authorize?.Invoke(db);if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path,overwrite:false);FailurePoint?.Invoke("Protected file published");return true;});
        }
        finally{try{File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
    internal void Delete(DeviceCredentialKind kind)
    {
        RequireCurrent();string path=FilePath(kind);RejectLinks(path);if(!Directory.Exists(DirectoryPath))return;using var gate=Gate();
        _store.Transaction(db=>{RequireScope(db);File.Delete(path);return true;});
    }
    private string FilePath(DeviceCredentialKind kind)
    {
        if(!Enum.IsDefined(kind))throw new ArgumentOutOfRangeException(nameof(kind));return Path.Combine(DirectoryPath,kind+".dat");
    }
    private byte[] Context(DeviceCredentialKind kind)=>Encoding.UTF8.GetBytes("SarahBeauty device credentials v1\n"+_scopeJson+"\n"+kind);
    private void RequireCurrent()=>_store.Read(db=>{RequireScope(db);return true;});
    private void RequireScope(DbWork db)
    {
        if(db.Scalar("SELECT Id FROM Business WHERE Id=$id;",("$id",_scope.BusinessId)) is null || (string?)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!=_scope.Mode)throw new UnauthorizedAccessException("These device credentials belong to another business context.");
    }
    private void EnsureDirectory(){RejectLinks(DirectoryPath);Directory.CreateDirectory(DirectoryPath);RejectLinks(DirectoryPath);}
    private FileStream Gate(){string path=Path.Combine(DirectoryPath,"credentials.lock");RejectLinks(path);return DeviceCredentialGate.Enter(path);}
    private void RejectLinks(string path)
    {
        foreach(string item in new[]{_root,DirectoryPath,path})
        {
            try{if((File.GetAttributes(item)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Device credential storage cannot use links or reparse points.");}
            catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
        }
    }
    private static bool Contains(string parent,string child)=>string.Equals(parent,child,StringComparison.OrdinalIgnoreCase)||child.StartsWith(Path.EndsInDirectorySeparator(parent)?parent:parent+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
    private static string DefaultRoot()
    {
        string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);if(string.IsNullOrWhiteSpace(local))throw new IOException("Windows could not locate private device credential storage.");return Path.Combine(local,"SarahBeautyDesktop","DeviceCredentials");
    }
}
