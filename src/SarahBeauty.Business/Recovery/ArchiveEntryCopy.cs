using System.IO.Compression;
using System.Security.Cryptography;

namespace SarahBeauty.Business.Recovery;

internal static class ArchiveEntryCopy
{
    internal static string Copy(ZipArchiveEntry entry,Stream destination,long expectedBytes,CancellationToken cancellation)
    {
        if(entry.Length!=expectedBytes)throw new InvalidDataException("A recovery entry size differs from its manifest.");
        using var input=entry.Open();using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] buffer=new byte[65536];long total=0;int count;
        while((count=input.Read(buffer))>0)
        {
            cancellation.ThrowIfCancellationRequested();total=checked(total+count);if(total>expectedBytes)throw new InvalidDataException("A recovery entry expands beyond its supported size.");destination.Write(buffer,0,count);hash.AppendData(buffer,0,count);
        }
        if(total!=expectedBytes)throw new InvalidDataException("A recovery entry is incomplete.");cancellation.ThrowIfCancellationRequested();return Convert.ToHexString(hash.GetHashAndReset());
    }
    internal static void File(ZipArchiveEntry entry,string destination,ArchiveFile expected,CancellationToken cancellation)
    {
        using var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        if(Copy(entry,output,expected.ByteLength,cancellation)!=expected.Sha256)throw new InvalidDataException("A recovery file failed its manifest hash check.");output.Flush(flushToDisk:true);
    }
}
