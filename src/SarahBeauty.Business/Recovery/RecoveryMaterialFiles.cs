using System.Security.Cryptography;

namespace SarahBeauty.Business.Recovery;

public static class RecoveryMaterialFiles
{
    public static async Task SaveAsync(RecoveryMaterial material,string destination,CancellationToken cancellation=default)
    {
        if(material.ProtectedIdentity.Length is <1 or >65536)throw new ArgumentException("The protected recovery file size is not supported.");byte[] bytes=material.ProtectedIdentity.ToArray();
        using var stage=new RecoveryOutputStage(destination);
        using(var output=new FileStream(stage.Path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){await output.WriteAsync(bytes,cancellation);output.Flush(flushToDisk:true);}
        await stage.Publish(new(){Ok=true,Bytes=bytes.Length,Sha256=Convert.ToHexString(SHA256.HashData(bytes))},cancellation);
    }
    public static async Task<byte[]> ReadAsync(string path,CancellationToken cancellation=default)
    {
        using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(input.Length is <1 or >65536)throw new InvalidDataException("Choose a supported protected recovery file of at most 64 KiB.");
        byte[] bytes=new byte[input.Length];await input.ReadExactlyAsync(bytes,cancellation);return bytes;
    }
}
