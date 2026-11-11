using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

public sealed class AutomaticBackups(BusinessStore store,TimeProvider? time=null)
{
    private readonly TimeProvider _time=time??TimeProvider.System;
    internal Action<string>? FailurePoint {get;set;}
    public BackupQueueState? Current(OwnerSession owner)=>store.Read(db=>{store.RequireSession(db,owner);return BackupQueueRecords.Read(db,owner.BusinessId,store.InstanceId);});
    public async Task<AutomaticBackupPoll> RunDueAsync(CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();BackupPermit permit;
        try{permit=new BackupSchedules(store).Authorize();}catch(UnauthorizedAccessException){return new("Not authorized",null);}
        using var lease=AutomaticArchiveLease.TryAcquire(store.DatabasePath);if(lease is null)return new("Busy",null);
        var state=Observe(permit);if(state.Status!="Running")return new(state.Status,state);
        try
        {
            var job=await new LocalBackups(store){FailurePoint=FailurePoint}.CreateAutomaticAsync(permit,cancellation);
            var completed=Finish(permit,job,null);return new(completed.Status,completed,job);
        }
        catch(Exception error){Finish(permit,null,error);throw;}
    }
    private BackupQueueState Observe(BackupPermit permit)=>store.Transaction(db=>
    {
        new BackupSchedules(store).Require(db,permit);string business=permit.Schedule.BusinessId;RecoverInterrupted(db,business);
        var jobs=BackupJobRecords.List(db,business).Where(j=>j.InstanceId==store.InstanceId && j.RecipientFingerprint==permit.RecipientFingerprint).ToList();
        var success=jobs.Where(j=>j.Status=="Succeeded").OrderByDescending(j=>j.Result!.Manifest.Watermark).FirstOrDefault();
        var prior=BackupQueueRecords.Read(db,business,store.InstanceId);bool same=prior?.ScheduleRevision==permit.Schedule.Revision && prior.RecipientFingerprint==permit.RecipientFingerprint;
        long watermark=Watermark(db,business);long? covered=success?.Result?.Manifest.Watermark;
        string? next=same?prior!.NextAttemptAtUtc:success?.FinishedAtUtc is string finished?DateTimeOffset.Parse(finished).AddMinutes(permit.Schedule.IntervalMinutes).ToString("O"):null;
        bool due=next is null || DateTimeOffset.Parse(next)<=_time.GetUtcNow();string status=covered>=watermark?"Succeeded":due?"Running":same && prior!.Status is "Failed" or "Cancelled"?prior.Status:"Pending";
        return BackupQueueRecords.Save(db,new(1,business,store.InstanceId,permit.Schedule.Revision,permit.RecipientFingerprint,watermark,covered,status,same && status!="Succeeded"?prior!.FailureCount:0,next,same?prior!.LastJobId:null,same && status!="Succeeded"?prior!.FailureCode:null));
    });
    private void RecoverInterrupted(DbWork db,string business)
    {
        foreach(var job in BackupJobRecords.List(db,business).Where(j=>j.InstanceId==store.InstanceId && j.AutomaticScheduleId is not null && j.Status=="Running"))
            BackupJobRecords.Complete(db,job with{Status="Interrupted",FinishedAtUtc=_time.GetUtcNow().ToString("O"),FailureCode="CompletionUnconfirmed"});
    }
    private BackupQueueState Finish(BackupPermit permit,LocalBackupJob? job,Exception? error)=>store.Transaction(db=>
    {
        string business=permit.Schedule.BusinessId;var prior=BackupQueueRecords.Read(db,business,store.InstanceId)??throw new InvalidDataException("The automatic archive queue is unavailable.");
        int failures=error is null?0:Math.Min(20,prior.FailureCount+1);long pending=Watermark(db,business);long? covered=job?.Result?.Manifest.Watermark??prior.ArchivedWatermark;
        string status=error is OperationCanceledException?"Cancelled":error is not null?"Failed":covered<pending?"Pending":"Succeeded";
        double minutes=error is null?permit.Schedule.IntervalMinutes:Math.Min(60,Math.Pow(2,failures-1));
        var attempt=job??BackupJobRecords.List(db,business).FirstOrDefault(j=>j.InstanceId==store.InstanceId && j.AutomaticScheduleId==permit.Schedule.Id);
        return BackupQueueRecords.Save(db,prior with{PendingWatermark=pending,ArchivedWatermark=covered,Status=status,FailureCount=failures,NextAttemptAtUtc=_time.GetUtcNow().AddMinutes(minutes).ToString("O"),LastJobId=attempt?.Id,FailureCode=error is null?null:attempt?.FailureCode??"CompletionUnconfirmed"});
    });
    private static long Watermark(DbWork db,string business)=>(long)db.Scalar("SELECT Watermark FROM Business WHERE Id=$id;",("$id",business))!;
}
