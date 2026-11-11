using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

public sealed class RecoveredStores
{
    internal Action<string>? FailurePoint {get;set;}
    public IReadOnlyList<(string Id,string Name)> Owners(StagedArchiveReview review)
    {
        using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=review.FilePath("sarahbeauty.db"),Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());connection.Open();return new DbWork(connection).Read("SELECT Id,Name FROM Owners WHERE Active=1 ORDER BY Name;",r=>(r.GetString(0),r.GetString(1)));
    }
    public async Task<PreparedRecoveredStore> PrepareAsync(StagedArchiveReview review,string destinationParent,RestoreCopyDecision decision,string newOwnerPassphrase,string requestId,CancellationToken cancellation=default)
    {
        if(!decision.PreviousPrimaryAcknowledged)throw new ArgumentException("Confirm that the previous writable instance is retired or permanently unavailable before preparing another writable copy.");
        if(string.IsNullOrWhiteSpace(decision.Reason) || decision.Reason.Length>4000)throw new ArgumentException("Enter a recovery reason of at most 4,000 characters.");
        if(!Guid.TryParse(requestId,out var request) || request==Guid.Empty)throw new ArgumentException("Use a new recovery request identity.");
        var manifest=review.VerifiedManifest(cancellation);if(manifest.Mode!="TEST")throw new ArgumentException("This build can prepare only TEST working stores. Preserve this archive for a build that supports its mode.");
        var credential=await Task.Run(()=>Passphrase.Hash(newOwnerPassphrase),cancellation);
        return await Task.Run(()=>Prepare(review,Path.GetFullPath(destinationParent),decision,credential,request.ToString(),manifest,cancellation),cancellation);
    }
    private PreparedRecoveredStore Prepare(StagedArchiveReview review,string parent,RestoreCopyDecision decision,(string Salt,string Hash) credential,string request,ArchiveManifest manifest,CancellationToken cancellation)
    {
        if(!Directory.Exists(parent))throw new DirectoryNotFoundException("Choose an existing private parent folder for the recovered store.");ArchivePaths.RejectLink(parent);
        string stores=Path.Combine(parent,"RestoredStores");ArchivePaths.RejectLink(stores);Directory.CreateDirectory(stores);string destination=Path.Combine(stores,Guid.Parse(request).ToString("N"));
        if(File.Exists(destination)||Directory.Exists(destination))throw new IOException("This recovery request already has a destination. Review that recovered copy before retrying.");
        using var workspace=new ArchiveWorkspace(parent);RecoveredStoreCopy.Write(review,workspace,manifest,cancellation);FailurePoint?.Invoke("Recovered files copied");
        var receipt=RecoveredAccess.Prepare(workspace.FilePath("sarahbeauty.db"),manifest,review,decision,credential,request,cancellation);FailurePoint?.Invoke("Recovered access prepared");
        RecoveredAccess.Verify(workspace.FilePath("sarahbeauty.db"),manifest,receipt);FailurePoint?.Invoke("Before recovered copy published");
        review.PublishPreparedCopy(()=>{cancellation.ThrowIfCancellationRequested();ArchivePaths.RejectLink(stores);Directory.Move(workspace.Root,destination);});return new(destination,receipt);
    }
}
