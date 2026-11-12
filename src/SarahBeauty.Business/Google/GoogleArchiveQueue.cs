using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed class GoogleArchiveQueue
{
    private readonly BusinessStore _store;
    private readonly GoogleOutputSettings _settings;
    private readonly GoogleArchiveDeliveryWorker _worker;
    private readonly GoogleArchiveHealthService _health;
    private readonly TimeProvider _time;
    public GoogleArchiveQueue(BusinessStore store,TimeProvider? time=null):this(store,new(store),new(store),time??TimeProvider.System){}
    internal GoogleArchiveQueue(BusinessStore store,GoogleOutputSettings settings,GoogleArchiveDeliveryWorker worker,TimeProvider time){_store=store;_settings=settings;_worker=worker;_health=new(store);_time=time;}
    public GoogleArchiveQueueState? Current(OwnerSession owner)=>_store.Read(db=>{_store.RequireSession(db,owner);return GoogleArchiveQueueRecords.Read(db,owner.BusinessId,_store.InstanceId);});
    public async Task<GoogleArchiveQueuePoll> RunDueAsync(CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();GoogleOutputPermit permit;try{permit=_settings.Authorize(GoogleOutputKind.DriveArchive);}catch(GoogleConnectionFailure){return new("Not authorized",null);}
        using var lease=GoogleArchiveDeliveryLease.TryQueue(_store);if(lease is null)return new("Busy",null);var state=Observe(permit);if(state.Status!="Running")return new(state.Status,state);
        try{var attempt=await _worker.DeliverCurrentAsync(state.LocalArchiveJobId!,cancellation).ConfigureAwait(false);var completed=Finish(state,null);return new(completed.Status,completed,attempt);}
        catch(Exception error){Finish(state,error);throw;}
    }
    private GoogleArchiveQueueState Observe(GoogleOutputPermit permit)=>_store.Transaction(db=>
    {
        _settings.Require(db,permit);var policy=permit.Policy;var profile=RecoveryProfiles.Read(db,policy.BusinessId);var health=_health.Read(db);
        var source=BackupJobRecords.List(db,policy.BusinessId).Where(j=>j.InstanceId==_store.InstanceId && j.Status=="Succeeded" && j.RecipientFingerprint==profile?.RecipientFingerprint && j.Result!.Manifest.StoreId==_store.StoreId && j.Result.Manifest.Watermark<=health.CurrentWatermark).OrderByDescending(j=>j.Result!.Manifest.Watermark).ThenByDescending(j=>j.FinishedAtUtc,StringComparer.Ordinal).FirstOrDefault();
        var prior=GoogleArchiveQueueRecords.Read(db,policy.BusinessId,_store.InstanceId);bool same=prior?.PolicyRevision==policy.Revision && prior.ConnectionId==policy.ConnectionId && prior.FolderBinding==policy.FolderBinding;
        bool covered=source is not null && health.ArchivedWatermark>=source.Result!.Manifest.Watermark;bool due=!same || prior!.NextAttemptAtUtc is null || DateTimeOffset.Parse(prior.NextAttemptAtUtc)<=_time.GetUtcNow();
        string status=source is null || covered && health.ArchivedWatermark<health.CurrentWatermark?"Waiting for local archive":covered?"Succeeded":due?"Running":prior!.Status is "Failed" or "Cancelled"?prior.Status:"Pending";
        return GoogleArchiveQueueRecords.Save(db,new(1,policy.BusinessId,_store.InstanceId,policy.Revision,policy.ConnectionId,policy.FolderBinding,health.CurrentWatermark,health.ArchivedWatermark,status,same && !covered?prior!.FailureCount:0,same && !covered?prior!.NextAttemptAtUtc:null,source?.Id,same && !covered?prior!.FailureCode:null));
    });
    private GoogleArchiveQueueState Finish(GoogleArchiveQueueState started,Exception? error)=>_store.Transaction(db=>
    {
        var current=GoogleArchiveQueueRecords.Read(db,started.BusinessId,_store.InstanceId);if(current!=started)throw new InvalidDataException("The Google archive retry state changed during delivery.");var health=_health.Read(db);int failures=error is null?0:Math.Min(20,started.FailureCount+1);
        string status=error is OperationCanceledException?"Cancelled":error is not null?"Failed":health.ArchivedWatermark>=health.CurrentWatermark?"Succeeded":"Waiting for local archive";
        return GoogleArchiveQueueRecords.Save(db,started with{ObservedWatermark=health.CurrentWatermark,DeliveredWatermark=health.ArchivedWatermark,Status=status,FailureCount=failures,NextAttemptAtUtc=error is null?null:_time.GetUtcNow().AddMinutes(Math.Min(60,Math.Pow(2,failures-1))).ToString("O"),FailureCode=error is null?null:GoogleDeliveryFailures.Code(error)});
    });
}
