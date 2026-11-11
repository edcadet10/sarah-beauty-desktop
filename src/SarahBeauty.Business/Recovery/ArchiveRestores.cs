using System.Security.Cryptography;

namespace SarahBeauty.Business.Recovery;

public sealed class ArchiveRestores
{
    internal Action<string>? FailurePoint {get;set;}
    public async Task<StagedArchiveReview> StageAsync(string encrypted,string reviewParent,byte[] protectedIdentity,string passphrase,CreatedArchive? expected=null,CancellationToken cancellation=default)
    {
        string source=Path.GetFullPath(encrypted);using var held=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read);
        const long maximumCipherBytes=ArchiveManifestChecks.MaximumBytes+(ArchiveManifestChecks.MaximumBytes/65536+1)*16+2*1024*1024+32;
        if(held.Length>maximumCipherBytes)throw new InvalidDataException("The selected archive exceeds the supported recovery size.");
        string hash=Convert.ToHexString(await SHA256.HashDataAsync(held,cancellation));if(expected is not null && (hash!=expected.Sha256 || held.Length!=expected.ByteLength))throw new InvalidDataException("The selected archive differs from its completed backup record.");
        if(!Directory.Exists(reviewParent))throw new DirectoryNotFoundException("Choose an existing private folder for recovery review.");var workspace=new ArchiveWorkspace(reviewParent);
        try
        {
            await new RecoveryCipher().DecryptAsync(source,workspace.PackagePath(),protectedIdentity,passphrase,cancellation);FailurePoint?.Invoke("Archive decrypted");
            var extracted=await Task.Run(()=>ArchiveExtraction.Read(workspace,cancellation,FailurePoint),cancellation);CheckRecorded(extracted,expected);
            await Task.Run(()=>RestoredDatabaseChecks.Verify(workspace.FilePath("sarahbeauty.db"),extracted.Manifest),cancellation);FailurePoint?.Invoke("Database verified");cancellation.ThrowIfCancellationRequested();
            return new(workspace,extracted,hash,held.Length,expected is not null);
        }
        catch{workspace.Dispose();throw;}
    }
    private static void CheckRecorded(ExtractedArchive extracted,CreatedArchive? expected)
    {
        if(expected is null)return;var actual=extracted.Manifest;var recorded=expected.Manifest;
        if(extracted.ManifestHash!=expected.ManifestHash || actual.BusinessId!=recorded.BusinessId || actual.StoreId!=recorded.StoreId || actual.Mode!=recorded.Mode || actual.Watermark!=recorded.Watermark)throw new InvalidDataException("The restored manifest differs from the completed backup record.");
    }
}
