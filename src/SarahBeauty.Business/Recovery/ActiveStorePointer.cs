using System.Text.Json;
using System.Text.Json.Serialization;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

public sealed record ActiveStoreSelection(int Version,string RequestId,string StoreId,string InstanceId,string RelativePath,string SelectedAtUtc,string Mode="TEST");
internal static class ActiveStorePointer
{
    internal static byte[]? ReadBytes(string path)
    {
        ArchivePaths.RejectLink(path);if(!File.Exists(path))return null;
        using var gate=ActivePointerGate.Enter(path);
        ArchivePaths.RejectLink(path);if(!File.Exists(path))return null;using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        if(file.Length is <1 or >32768)throw new InvalidDataException("The active-store selection is invalid. Preserve the stored copies and review recovery.");byte[] bytes=new byte[file.Length];file.ReadExactly(bytes);return bytes;
    }
    internal static ActiveStoreSelection? Read(RecoveredStorePaths paths)
    {
        var bytes=ReadBytes(paths.Pointer);if(bytes is null)return null;ActiveStoreSelection value;
        try{value=JsonSerializer.Deserialize<ActiveStoreSelection>(bytes,new JsonSerializerOptions{UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,RespectRequiredConstructorParameters=true})??throw new JsonException();}
        catch(JsonException error){throw new InvalidDataException("The active-store selection needs recovery review.",error);}
        if(value.Version!=1 ||value.Mode!=BusinessModes.StoredName(paths.Mode)|| !Guid.TryParse(value.RequestId,out var request)||request==Guid.Empty || value.RelativePath!=paths.Relative(value.RequestId) || !Guid.TryParse(value.StoreId,out var store)||store==Guid.Empty || !Guid.TryParseExact(value.InstanceId,"N",out var instance)||instance==Guid.Empty)throw new InvalidDataException("The active-store selection mode, path or identity is invalid.");return value;
    }
    internal static void Write(string path,byte[] bytes)
    {
        using var gate=ActivePointerGate.Enter(path);
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".partial";
        try
        {
            using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes);output.Flush(flushToDisk:true);}
            ArchivePaths.RejectLink(path);if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path,overwrite:false);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    internal static void Clear(string path){using var gate=ActivePointerGate.Enter(path);ArchivePaths.RejectLink(path);File.Delete(path);}
}
