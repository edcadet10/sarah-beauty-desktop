using System.Collections.Concurrent;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

public sealed class LocalBackups(BusinessStore store)
{
    private static readonly ConcurrentDictionary<string,byte> ActiveJobs=new();
    internal Action<string>? FailurePoint {get;set;}
    public IReadOnlyList<LocalBackupJob> History(OwnerSession owner)=>store.Read(db=>{store.RequireSession(db,owner);return BackupJobRecords.List(db,owner.BusinessId);});
    public LocalBackupHealth Health(OwnerSession owner)=>store.ReadSnapshot(db=>
    {
        store.RequireSession(db,owner);var jobs=BackupJobRecords.List(db,owner.BusinessId);var latest=jobs.FirstOrDefault();var success=jobs.Where(j=>j.Status=="Succeeded").OrderByDescending(j=>j.Result!.Manifest.Watermark).ThenByDescending(j=>j.FinishedAtUtc,StringComparer.Ordinal).FirstOrDefault();
        long current=(long)db.Scalar("SELECT Watermark FROM Business WHERE Id=$id;",("$id",owner.BusinessId))!;long? covered=success?.Result?.Manifest.Watermark;
        string status=jobs.Any(j=>j.Status=="Running" && ActiveJobs.ContainsKey(j.Id))?"Running":jobs.Any(j=>j.Status=="Running")?"Needs review":latest?.Status is "Failed" or "Cancelled"?latest.Status:covered is null?"Never completed":covered<current?"Pending":"Succeeded";
        return new LocalBackupHealth(status,current,covered,success?.FinishedAtUtc,latest,success);
    });
    public async Task<LocalBackupJob> CreateAsync(OwnerSession owner,string destination,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();var started=Start(owner,Path.GetFullPath(destination));ActiveJobs.TryAdd(started.Job.Id,0);
        try
        {
            var result=await RunArchive(owner,started.Job,started.Profile,cancellation);var completed=started.Job with{Status="Succeeded",FinishedAtUtc=DateTimeOffset.UtcNow.ToString("O"),Result=result};
            try{FailurePoint?.Invoke("History completion");Finish(completed);}
            catch(Exception error){throw new IOException("An encrypted archive was created, but its history could not be saved. Verify the selected file before retrying.",error);}
            return completed;
        }
        finally{ActiveJobs.TryRemove(started.Job.Id,out _);}
    }
    private async Task<CreatedArchive> RunArchive(OwnerSession owner,LocalBackupJob job,RecoveryProfile profile,CancellationToken cancellation)
    {
        try{return await new FullArchives(store){FailurePoint=FailurePoint}.CreateAsync(owner,job.Destination,profile.Recipient,cancellation);}
        catch(Exception error)
        {
            var failed=job with{Status=error is OperationCanceledException?"Cancelled":"Failed",FinishedAtUtc=DateTimeOffset.UtcNow.ToString("O"),FailureCode=Code(error)};
            try{Finish(failed);}catch(Exception recordError) when(recordError is IOException or Microsoft.Data.Sqlite.SqliteException or InvalidDataException){throw new IOException("The archive attempt ended, but its history could not be saved. Review the selected destination before retrying.",error);}
            throw;
        }
    }
    private (LocalBackupJob Job,RecoveryProfile Profile) Start(OwnerSession owner,string destination)=>store.Transaction(db=>
    {
        store.RequireSession(db,owner);var profile=RecoveryProfiles.Read(db,owner.BusinessId)??throw new ArgumentException("Save and confirm portable recovery material before creating an archive.");
        var job=new LocalBackupJob(1,Guid.NewGuid().ToString(),owner.BusinessId,"Running",destination,profile.RecipientFingerprint,(long)db.Scalar("SELECT Watermark FROM Business WHERE Id=$id;",("$id",owner.BusinessId))!,DateTimeOffset.UtcNow.ToString("O"),InstanceId:store.InstanceId);BackupJobRecords.Insert(db,job);return(job,profile);
    });
    private void Finish(LocalBackupJob job)=>store.Transaction(db=>{BackupJobRecords.Complete(db,job);return true;});
    private static string Code(Exception error)=>error switch{OperationCanceledException=>"Cancelled",RecoveryFailure failure=>failure.Code,InvalidDataException=>"VerificationFailed",IOException=>"LocalFileUnavailable",UnauthorizedAccessException=>"AccessUnavailable",_=>"OperationFailed"};
}
