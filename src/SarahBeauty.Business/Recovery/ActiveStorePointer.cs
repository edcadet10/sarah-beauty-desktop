using System.Text.Json;
using System.Text.Json.Serialization;

namespace SarahBeauty.Business.Recovery;

public sealed record ActiveTestSelection(int Version,string RequestId,string StoreId,string InstanceId,string RelativePath,string SelectedAtUtc);
internal static class ActiveStorePointer
{
    internal static byte[]? ReadBytes(string path)
    {
        using var gate=ActivePointerGate.Enter(path);
        ArchivePaths.RejectLink(path);if(!File.Exists(path))return null;using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        if(file.Length is <1 or >32768)throw new InvalidDataException("The active-store selection is invalid. Preserve the stored copies and review recovery.");byte[] bytes=new byte[file.Length];file.ReadExactly(bytes);return bytes;
    }
    internal static ActiveTestSelection? Read(RecoveredStorePaths paths)
    {
        var bytes=ReadBytes(paths.Pointer);if(bytes is null)return null;ActiveTestSelection value;
        try{value=JsonSerializer.Deserialize<ActiveTestSelection>(bytes,new JsonSerializerOptions{UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,RespectRequiredConstructorParameters=true})??throw new JsonException();}
        catch(JsonException error){throw new InvalidDataException("The active-store selection needs recovery review.",error);}
        if(value.Version!=1 || !Guid.TryParse(value.RequestId,out _) || value.RelativePath!=paths.Relative(value.RequestId) || !Guid.TryParse(value.StoreId,out _) || !Guid.TryParseExact(value.InstanceId,"N",out _))throw new InvalidDataException("The active-store selection path or identity is invalid.");return value;
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
