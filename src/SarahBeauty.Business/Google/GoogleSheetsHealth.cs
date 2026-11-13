using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed record GoogleSheetsOutcome(GoogleSheetsExportJob Job,GoogleSheetsAttempt Attempt);
public sealed record GoogleSheetsHealth(string Status,long CurrentWatermark,ReadableExportSelection? Selection,GoogleSheetsOutcome? LastAttempt,GoogleSheetsOutcome? LastSuccess,GoogleSheetsOutcome? MatchingSuccess,string? FailureCode=null)
{
    public long? ExportedWatermark=>MatchingSuccess?.Job.SourceWatermark;
    public string? CompletedAtUtc=>MatchingSuccess?.Attempt.FinishedAtUtc;
}

public sealed class GoogleSheetsHealthService(BusinessStore store)
{
    public GoogleSheetsHealth Current(OwnerSession owner,ReadableExportSelection? selection=null)=>store.ReadSnapshot(db=>{store.RequireSession(db,owner);return Read(db,selection);});
    internal GoogleSheetsHealth Current(ReadableExportSelection? selection=null)=>store.ReadSnapshot(db=>Read(db,selection));
    internal GoogleSheetsHealth Read(DbWork db,ReadableExportSelection? selection)
    {
        selection?.Validate();string business=db.Read("SELECT Id FROM Business;",r=>r.GetString(0)).Single();long watermark=(long)db.Scalar("SELECT Watermark FROM Business WHERE Id=$id;",("$id",business))!;
        var policy=GoogleOutputRecords.Read(db,business,store.InstanceId);if(policy is null)return new("Not configured",watermark,selection,null,null,null);
        var jobs=GoogleSheetsJobRecords.List(db,business).Where(job=>job.StoreId==store.StoreId&&job.InstanceId==store.InstanceId&&job.ConnectionId==policy.ConnectionId&&job.FolderId==policy.FolderId&&job.FolderBinding==policy.FolderBinding).ToDictionary(job=>job.Id);
        if(jobs.Values.Any(job=>job.SourceWatermark>watermark))throw GoogleSheetsAttemptRecords.Invalid();
        var outcomes=GoogleSheetsAttemptRecords.List(db,business).Where(a=>a.InstanceId==store.InstanceId&&jobs.ContainsKey(a.JobId)).Select(a=>new GoogleSheetsOutcome(jobs[a.JobId],a)).ToArray();
        var successes=outcomes.Where(outcome=>outcome.Attempt.Status=="Succeeded").OrderByDescending(outcome=>outcome.Job.SourceWatermark).ThenByDescending(outcome=>outcome.Attempt.FinishedAtUtc,StringComparer.Ordinal).ToArray();
        foreach(var success in successes)RequireProof(db,success);
        var latest=outcomes.OrderByDescending(outcome=>outcome.Attempt.StartedAtUtc,StringComparer.Ordinal).ThenByDescending(outcome=>outcome.Attempt.Id,StringComparer.Ordinal).FirstOrDefault();
        var matching=successes.FirstOrDefault(outcome=>selection is not null&&outcome.Job.Selection==selection&&outcome.Job.SchemaVersion==BusinessSchema.Version);
        var health=new GoogleSheetsHealth("Never completed",watermark,selection,latest,successes.FirstOrDefault(),matching);
        if(!policy.SheetsExportEnabled)return health with{Status="Disabled"};var connection=GoogleConnectionRecords.Read(db,business,store.InstanceId);
        try
        {
            if(connection is null)throw new GoogleConnectionFailure("GoogleConsentUnavailable","Google connection permission is unavailable.");new GoogleOutputSettings(store).Require(db,new(policy,connection,GoogleOutputKind.SheetsExport));
        }
        catch(GoogleConnectionFailure error){return health with{Status="Permission required",FailureCode=error.Code};}
        if(outcomes.Any(outcome=>outcome.Attempt.Status=="Running"&&GoogleSheetsDeliveryWorker.IsActive(outcome.Attempt.Id)))return health with{Status="Running"};
        if(outcomes.Any(outcome=>outcome.Attempt.Status=="Running")||latest?.Attempt.Status=="Interrupted")return health with{Status="Needs review"};
        if(latest?.Attempt.Status is "Failed" or "Cancelled")return health with{Status=latest.Attempt.Status,FailureCode=latest.Attempt.FailureCode};
        return health with{Status=selection is null?"Report dates required":matching is null?"Never completed":matching.Job.SourceWatermark<watermark?"Pending":"Succeeded"};
    }
    private static void RequireProof(DbWork db,GoogleSheetsOutcome outcome)
    {
        var creation=GoogleSheetsCreationRecords.Get(db,outcome.Job);var publication=GoogleSheetsPublicationRecords.Get(db,outcome.Job);
        if(creation is not{State:"FileKnown"}||creation.FileId!=outcome.Attempt.FileId||publication is null||publication.FileId!=outcome.Attempt.FileId)throw GoogleSheetsAttemptRecords.Invalid();
    }
}
