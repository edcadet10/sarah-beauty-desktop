using System.Security.Cryptography;

namespace SarahBeauty.Business.Recovery;

internal static class RecoveredStoreCopy
{
    internal static void Write(StagedArchiveReview review,ArchiveWorkspace workspace,ArchiveManifest manifest,CancellationToken cancellation)
    {
        foreach(var file in manifest.Files)
        {
            cancellation.ThrowIfCancellationRequested();using var input=new FileStream(review.FilePath(file.RelativePath),FileMode.Open,FileAccess.Read,FileShare.Read);
            using var output=new FileStream(workspace.FilePath(file.RelativePath),FileMode.CreateNew,FileAccess.Write,FileShare.None);using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] buffer=new byte[65536];long total=0;int count;
            while((count=input.Read(buffer))>0){cancellation.ThrowIfCancellationRequested();total=checked(total+count);if(total>file.ByteLength)throw new InvalidDataException("A staged recovery file changed before copying.");output.Write(buffer,0,count);hash.AppendData(buffer,0,count);}
            if(total!=file.ByteLength || Convert.ToHexString(hash.GetHashAndReset())!=file.Sha256)throw new InvalidDataException("A staged recovery file failed its final copy hash check.");output.Flush(flushToDisk:true);
        }
        RestoredDatabaseChecks.Verify(workspace.FilePath("sarahbeauty.db"),manifest);cancellation.ThrowIfCancellationRequested();
    }
}
