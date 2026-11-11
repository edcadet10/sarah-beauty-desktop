using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace SarahBeauty.Business.Recovery;

internal sealed record PreparedArchivePackage(string Path,string ManifestHash,string PackageHash,long ByteLength);
internal static class ArchivePackage
{
    internal const int MaximumManifestBytes=32*1024*1024;
    internal static PreparedArchivePackage Write(ArchiveCapture captured,CancellationToken cancellation)
    {
        byte[] manifest=JsonSerializer.SerializeToUtf8Bytes(captured.Manifest);if(manifest.Length>MaximumManifestBytes)throw new InvalidDataException("The recovery manifest exceeds the supported size.");
        string path=captured.PackagePath();
        using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
        {
            using(var zip=new ZipArchive(output,ZipArchiveMode.Create,leaveOpen:true))
            {
                using(var entry=zip.CreateEntry("manifest.json",CompressionLevel.Fastest).Open())entry.Write(manifest);
                foreach(var file in captured.Manifest.Files){cancellation.ThrowIfCancellationRequested();Add(zip,captured,file,cancellation);}
            }
            output.Flush(flushToDisk:true);
        }
        using var package=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(package.Length>64L*1024*1024*1024)throw new InvalidDataException("The recovery package exceeds the supported size.");
        return new(path,Convert.ToHexString(SHA256.HashData(manifest)),Convert.ToHexString(SHA256.HashData(package)),package.Length);
    }
    private static void Add(ZipArchive zip,ArchiveCapture captured,ArchiveFile file,CancellationToken cancellation)
    {
        using var input=new FileStream(captured.FilePath(file.RelativePath),FileMode.Open,FileAccess.Read,FileShare.Read);using var output=zip.CreateEntry(ArchivePaths.Normalize(file.RelativePath),CompressionLevel.Fastest).Open();
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] buffer=new byte[65536];long total=0;int count;
        while((count=input.Read(buffer))>0){cancellation.ThrowIfCancellationRequested();total=checked(total+count);if(total>file.ByteLength)throw new InvalidDataException("A captured recovery file grew before packaging.");output.Write(buffer,0,count);hash.AppendData(buffer,0,count);}
        if(total!=file.ByteLength || Convert.ToHexString(hash.GetHashAndReset())!=file.Sha256)throw new InvalidDataException("A captured recovery file changed before packaging.");
    }
}
