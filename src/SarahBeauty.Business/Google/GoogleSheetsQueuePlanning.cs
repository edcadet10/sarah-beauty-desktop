using System.Globalization;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

internal sealed record GoogleSheetsQueueDecision(GoogleSheetsQueueState State,bool Ready,GoogleSheetsExportJob? Job);
internal sealed class GoogleSheetsQueuePlanning(BusinessStore store,TimeProvider time)
{
    internal GoogleSheetsQueueDecision Observe(GoogleOutputPermit permit,GoogleSheetsExportProfile profile,CancellationToken cancellation=default)=>store.Transaction(db=>
    {
        cancellation.ThrowIfCancellationRequested();Require(db,permit,profile);var health=new GoogleSheetsHealthService(store).Read(db,profile.Selection);var prior=GoogleSheetsQueueRecords.Read(db,profile.BusinessId,store.InstanceId);
        var attempts=GoogleSheetsAttemptRecords.List(db,profile.BusinessId);var jobs=Jobs(db,permit);var completed=attempts.Where(a=>a.Status=="Succeeded").Select(a=>a.JobId).ToHashSet(StringComparer.Ordinal);
        var pending=jobs.Where(job=>!completed.Contains(job.Id)).Select(job=>(Job:job,Creation:GoogleSheetsCreationRecords.Get(db,job)))
            .Where(value=>value.Job.Selection==profile.Selection||value.Creation?.State is "CreateUncertain" or "FileKnown")
            .OrderByDescending(value=>value.Job.Id==prior?.JobId).ThenByDescending(value=>value.Creation?.State is "CreateUncertain" or "FileKnown").ThenBy(value=>value.Job.CreatedAtUtc,StringComparer.Ordinal).Select(value=>value.Job).FirstOrDefault();
        var active=attempts.FirstOrDefault(a=>a.Status=="Running"&&GoogleSheetsDeliveryWorker.IsActive(a.Id)&&jobs.Any(job=>job.Id==a.JobId));
        bool same=prior?.ProfileRevision==profile.Revision&&prior.PolicyRevision==permit.Policy.Revision&&prior.ConnectionId==permit.Policy.ConnectionId&&prior.FolderBinding==permit.Policy.FolderBinding;
        bool interrupted=same&&prior!.Status is ("Preparing" or "Running");var next=interrupted?null:Next(prior,same,health,profile);
        bool due=next is null||next<=time.GetUtcNow();bool covered=health.ExportedWatermark==health.CurrentWatermark;
        string status=Status(active,pending,covered,due,same,prior);
        bool ready=active is null&&status is ("Preparing" or "Running");string? id=active?.JobId??(status=="Succeeded"?null:pending?.Id);
        var state=new GoogleSheetsQueueState(1,profile.BusinessId,store.InstanceId,profile.Revision,permit.Policy.Revision,permit.Policy.ConnectionId,permit.Policy.FolderBinding,health.CurrentWatermark,health.ExportedWatermark,
            status,same&&status!="Succeeded"?prior!.FailureCount:0,next?.ToString("O"),id,status is "Failed" or "Cancelled"?prior!.FailureCode:status=="Needs review"?"GoogleSheetsSourceChanged":null,time.GetUtcNow().ToString("O"));
        cancellation.ThrowIfCancellationRequested();var saved=GoogleSheetsQueueRecords.Put(db,prior,state);cancellation.ThrowIfCancellationRequested();return new GoogleSheetsQueueDecision(saved,ready,pending);
    });

    internal GoogleSheetsQueueDecision Attach(GoogleOutputPermit permit,GoogleSheetsExportProfile profile,GoogleSheetsQueueDecision expected,GoogleSheetsExportJob job,CancellationToken cancellation=default)=>store.Transaction(db=>
    {
        cancellation.ThrowIfCancellationRequested();Require(db,permit,profile);new GoogleSheetsExportJobs(store).Require(db,permit,job);
        if(!expected.Ready||expected.State.Status is not("Preparing" or "Running")||expected.State.JobId is not null&&expected.State.JobId!=job.Id||expected.State.JobId is null&&job.Selection!=profile.Selection)throw GoogleSheetsQueueRecords.Invalid();
        var next=expected.State with{Status="Running",JobId=job.Id,UpdatedAtUtc=time.GetUtcNow().ToString("O")};GoogleSheetsQueueRecords.Put(db,expected.State,next);cancellation.ThrowIfCancellationRequested();return new GoogleSheetsQueueDecision(next,true,job);
    });

    private IReadOnlyList<GoogleSheetsExportJob> Jobs(DbWork db,GoogleOutputPermit permit)=>GoogleSheetsJobRecords.List(db,permit.Policy.BusinessId)
        .Where(job=>job.StoreId==store.StoreId&&job.InstanceId==store.InstanceId&&job.ConnectionId==permit.Policy.ConnectionId&&job.FolderId==permit.Policy.FolderId&&job.FolderBinding==permit.Policy.FolderBinding).ToArray();
    private static string Status(GoogleSheetsAttempt? active,GoogleSheetsExportJob? pending,bool covered,bool due,bool same,GoogleSheetsQueueState? prior)
    {
        if(active is not null)return "Running";if(pending is not null&&pending.SchemaVersion!=BusinessSchema.Version)return "Needs review";
        if(covered&&pending is null)return "Succeeded";if(due)return pending is null?"Preparing":"Running";
        if(same&&prior!.Status is ("Failed" or "Cancelled"))return prior.Status;return "Pending";
    }
    private static DateTimeOffset? Next(GoogleSheetsQueueState? prior,bool same,GoogleSheetsHealth health,GoogleSheetsExportProfile profile)
    {
        DateTimeOffset? next=same&&prior?.NextAttemptAtUtc is string retry?DateTimeOffset.ParseExact(retry,"O",CultureInfo.InvariantCulture):null;
        if(health.CompletedAtUtc is string completed)
        {
            var earliest=DateTimeOffset.ParseExact(completed,"O",CultureInfo.InvariantCulture).AddMinutes(profile.MinimumIntervalMinutes);if(next is null||earliest>next)next=earliest;
        }
        return next;
    }
    private void Require(DbWork db,GoogleOutputPermit permit,GoogleSheetsExportProfile profile)
    {
        if(permit.Kind!=GoogleOutputKind.SheetsExport||permit.Policy.BusinessId!=profile.BusinessId)throw new UnauthorizedAccessException("The automatic readable profile and Sheets permission must match.");
        new GoogleOutputSettings(store).Require(db,permit);new GoogleSheetsExportProfiles(store).Require(db,profile);
    }
}
