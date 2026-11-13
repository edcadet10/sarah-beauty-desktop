using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed class GoogleSheetsQueue
{
    private readonly BusinessStore _store;
    private readonly GoogleOutputSettings _settings;
    private readonly GoogleSheetsDeliveryWorker _worker;
    private readonly TimeProvider _time;
    public GoogleSheetsQueue(BusinessStore store,TimeProvider? time=null):this(store,new(store),new(store),time??TimeProvider.System){}
    internal GoogleSheetsQueue(BusinessStore store,GoogleOutputSettings settings,GoogleSheetsDeliveryWorker worker,TimeProvider time){_store=store;_settings=settings;_worker=worker;_time=time;}
    public GoogleSheetsQueueState? Current(OwnerSession owner)=>_store.Read(db=>{_store.RequireSession(db,owner);return GoogleSheetsQueueRecords.Read(db,owner.BusinessId,_store.InstanceId);});
    public async Task<GoogleSheetsQueuePoll> RunDueAsync(CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();if(!OutputEnabled())return new("Sheets export disabled or not configured",null);GoogleOutputPermit permit;GoogleSheetsExportProfile? profile;
        try{permit=_settings.Authorize(GoogleOutputKind.SheetsExport);profile=Profile(permit);}catch(GoogleConnectionFailure){return new("Permission required",null);}
        if(profile is null)return new("Automatic export disabled or not configured",null);
        using var lease=GoogleSheetsDeliveryLease.TryQueue(_store);if(lease is null)return new("Busy",null);
        var planning=new GoogleSheetsQueuePlanning(_store,_time);var decision=planning.Observe(permit,profile,cancellation);if(!decision.Ready)return new(decision.State.Status,decision.State);
        GoogleSheetsAttempt attempt;
        try
        {
            var job=decision.Job??new GoogleSheetsExportJobs(_store).Capture(permit,profile.Selection,cancellation);decision=planning.Attach(permit,profile,decision,job,cancellation);
            attempt=await _worker.RetryScheduledAsync(job.Id,profile,cancellation).ConfigureAwait(false);
        }
        catch(Exception error){Finish(decision.State,profile,error);throw;}
        var completed=Finish(decision.State,profile,null);return new(completed.Status,completed,attempt);
    }
    private GoogleSheetsExportProfile? Profile(GoogleOutputPermit permit)=>_store.Read(db=>
    {
        var profile=GoogleSheetsProfileRecords.Read(db,permit.Policy.BusinessId,_store.InstanceId);if(profile is null||!profile.AutomaticEnabled)return null;
        new GoogleSheetsExportProfiles(_store).Require(db,profile);return profile;
    });
    private bool OutputEnabled()=>_store.Read(db=>
    {
        string business=db.Read("SELECT Id FROM Business;",r=>r.GetString(0)).Single();return GoogleOutputRecords.Read(db,business,_store.InstanceId)?.SheetsExportEnabled==true;
    });
    private GoogleSheetsQueueState Finish(GoogleSheetsQueueState started,GoogleSheetsExportProfile profile,Exception? error)=>_store.Transaction(db=>
    {
        var health=new GoogleSheetsHealthService(_store).Read(db,profile.Selection);var currentProfile=GoogleSheetsProfileRecords.Read(db,profile.BusinessId,_store.InstanceId);var policy=GoogleOutputRecords.Read(db,profile.BusinessId,_store.InstanceId);
        bool disabled=currentProfile?.AutomaticEnabled!=true||policy?.SheetsExportEnabled!=true;
        bool same=currentProfile==profile&&policy?.Revision==started.PolicyRevision&&policy.ConnectionId==started.ConnectionId&&policy.FolderBinding==started.FolderBinding;
        int failures=error is null?0:Math.Min(20,started.FailureCount+1);int interval=currentProfile?.MinimumIntervalMinutes??profile.MinimumIntervalMinutes;
        double minutes=error is null?interval:Math.Max(interval,Math.Min(60,Math.Pow(2,failures-1)));
        string status=disabled?"Disabled":error is OperationCanceledException?"Cancelled":error is not null?"Failed":same&&health.ExportedWatermark==health.CurrentWatermark?"Succeeded":"Pending";
        return GoogleSheetsQueueRecords.Put(db,started,started with{ObservedWatermark=health.CurrentWatermark,DeliveredWatermark=health.ExportedWatermark,Status=status,FailureCount=failures,
            NextAttemptAtUtc=_time.GetUtcNow().AddMinutes(minutes).ToString("O"),JobId=status=="Succeeded"?null:started.JobId,FailureCode=error is null?null:GoogleSheetsDeliveryFailures.Code(error),UpdatedAtUtc=_time.GetUtcNow().ToString("O")});
    });
}
