using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed class GoogleSheetsDeliveries(BusinessStore store)
{
    public IReadOnlyList<GoogleSheetsDeliveryHistory> History(OwnerSession owner)=>store.ReadSnapshot(db=>
    {
        store.RequireSession(db,owner);var attempts=GoogleSheetsAttemptRecords.List(db,owner.BusinessId);
        return GoogleSheetsJobRecords.List(db,owner.BusinessId).OrderByDescending(job=>job.CreatedAtUtc,StringComparer.Ordinal).ThenBy(job=>job.Id,StringComparer.Ordinal)
            .Select(job=>new GoogleSheetsDeliveryHistory(job,GoogleSheetsCreationRecords.Get(db,job),attempts.Where(a=>a.JobId==job.Id).OrderBy(a=>a.StartedAtUtc,StringComparer.Ordinal).ThenBy(a=>a.Id,StringComparer.Ordinal).ToArray())).ToArray();
    });

    internal GoogleSheetsAttempt Begin(GoogleOutputPermit permit,GoogleSheetsExportJob job,CancellationToken cancellation=default)=>store.Transaction(db=>
    {
        cancellation.ThrowIfCancellationRequested();new GoogleSheetsExportJobs(store).Require(db,permit,job);var prior=GoogleSheetsAttemptRecords.List(db,job.BusinessId).Where(a=>a.JobId==job.Id).ToArray();
        if(prior.Any(a=>a.Status=="Running"))throw new GoogleConnectionFailure("GoogleSheetsBusy","A prior readable attempt is still running or needs interrupted-state review.");
        if(prior.Any(a=>a.Status=="Succeeded"))throw new GoogleConnectionFailure("GoogleSheetsAlreadyVerified","This export was already verified. Create a new generation to publish newer records.");
        var attempt=new GoogleSheetsAttempt(1,Guid.NewGuid().ToString("N"),job.Id,job.BusinessId,store.InstanceId,permit.Policy.Revision,"Running",DateTimeOffset.UtcNow.ToString("O"));
        GoogleSheetsAttemptRecords.Insert(db,attempt);cancellation.ThrowIfCancellationRequested();return attempt;
    });

    internal GoogleSheetsAttempt Complete(GoogleOutputPermit permit,GoogleSheetsExportJob job,GoogleSheetsAttempt attempt,GoogleSheetsVerifiedDelivery result,CancellationToken cancellation=default)=>store.Transaction(db=>
    {
        cancellation.ThrowIfCancellationRequested();RequireRunning(db,permit,job,attempt);var creation=GoogleSheetsCreationRecords.Get(db,job);
        var publication=GoogleSheetsPublicationRecords.Get(db,job);if(publication is null||publication.FileId!=result.FileId)throw GoogleSheetsPublicationRecords.Invalid();
        if(creation is not{State:"FileKnown"}||creation.FileId!=result.FileId||result.GenerationId!=job.GenerationId||result.ManifestSha256!=job.ManifestSha256||result.SourceWatermark!=job.SourceWatermark||!GoogleSheetsAttemptRecords.Utc(result.VerifiedAtUtc))throw GoogleSheetsAttemptRecords.Invalid();
        var completed=attempt with{Status="Succeeded",FinishedAtUtc=DateTimeOffset.UtcNow.ToString("O"),FileId=result.FileId,VerifiedAtUtc=result.VerifiedAtUtc};
        GoogleSheetsAttemptRecords.Replace(db,attempt,completed);cancellation.ThrowIfCancellationRequested();return completed;
    });

    internal GoogleSheetsAttempt Fail(GoogleSheetsAttempt attempt,Exception error)=>store.Transaction(db=>Finish(db,attempt,error is OperationCanceledException?"Cancelled":"Failed",GoogleSheetsDeliveryFailures.Code(error)));

    // Call only while holding the store's delivery lease; persisted Running is not proof of a live process.
    internal void InterruptRunning()=>store.Transaction(db=>
    {
        string business=db.Read("SELECT Id FROM Business;",r=>r.GetString(0)).Single();
        foreach(var attempt in GoogleSheetsAttemptRecords.List(db,business).Where(a=>a.InstanceId==store.InstanceId&&a.Status=="Running"))Finish(db,attempt,"Interrupted","CompletionUnconfirmed");return true;
    });

    internal void RequireRunning(DbWork db,GoogleOutputPermit permit,GoogleSheetsExportJob job,GoogleSheetsAttempt attempt)
    {
        new GoogleSheetsExportJobs(store).Require(db,permit,job);
        if(attempt.JobId!=job.Id||attempt.BusinessId!=job.BusinessId||attempt.InstanceId!=store.InstanceId||attempt.PolicyRevision!=permit.Policy.Revision||attempt.Status!="Running"||GoogleSheetsAttemptRecords.List(db,job.BusinessId).SingleOrDefault(a=>a.Id==attempt.Id)!=attempt)throw GoogleSheetsAttemptRecords.Invalid();
    }
    private GoogleSheetsAttempt Finish(DbWork db,GoogleSheetsAttempt attempt,string status,string code)
    {
        if(attempt.InstanceId!=store.InstanceId||attempt.Status!="Running"||GoogleSheetsJobRecords.Get(db,attempt.BusinessId,attempt.JobId).InstanceId!=store.InstanceId)throw GoogleSheetsAttemptRecords.Invalid();
        var completed=attempt with{Status=status,FinishedAtUtc=DateTimeOffset.UtcNow.ToString("O"),FailureCode=code};GoogleSheetsAttemptRecords.Replace(db,attempt,completed);return completed;
    }
}
