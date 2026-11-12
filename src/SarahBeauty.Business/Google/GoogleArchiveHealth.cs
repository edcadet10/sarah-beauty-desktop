using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed record GoogleArchiveOutcome(GoogleArchiveDelivery Delivery,GoogleArchiveAttempt Attempt);
public sealed record GoogleArchiveHealth(string Status,long CurrentWatermark,GoogleArchiveOutcome? LastAttempt,GoogleArchiveOutcome? LastSuccess,string? FailureCode=null)
{
    public long? ArchivedWatermark=>LastSuccess?.Delivery.ArchiveWatermark;
    public string? CompletedAtUtc=>LastSuccess?.Attempt.FinishedAtUtc;
}
public sealed class GoogleArchiveHealthService(BusinessStore store)
{
    private readonly GoogleOutputSettings _settings=new(store);
    public GoogleArchiveHealth Current(OwnerSession owner)=>store.ReadSnapshot(db=>{store.RequireSession(db,owner);return Read(db);});
    internal GoogleArchiveHealth Current()=>store.ReadSnapshot(Read);
    private GoogleArchiveHealth Read(DbWork db)
    {
        string business=db.Read("SELECT Id FROM Business;",r=>r.GetString(0)).Single();long watermark=(long)db.Scalar("SELECT Watermark FROM Business WHERE Id=$id;",("$id",business))!;var policy=GoogleOutputRecords.Read(db,business,store.InstanceId);if(policy is null)return new("Not configured",watermark,null,null);
        var profile=RecoveryProfiles.Read(db,business);var deliveries=GoogleArchiveDeliveryRecords.Deliveries(db,business).Where(d=>d.InstanceId==store.InstanceId && d.ConnectionId==policy.ConnectionId && d.FolderId==policy.FolderId && d.FolderBinding==policy.FolderBinding && d.RecipientFingerprint==profile?.RecipientFingerprint).ToDictionary(d=>d.Id);
        var outcomes=GoogleArchiveDeliveryRecords.Attempts(db,business).Where(a=>a.InstanceId==store.InstanceId && deliveries.ContainsKey(a.DeliveryId)).Select(a=>new GoogleArchiveOutcome(deliveries[a.DeliveryId],a)).ToList();
        var latest=outcomes.OrderByDescending(o=>o.Attempt.StartedAtUtc,StringComparer.Ordinal).ThenByDescending(o=>o.Attempt.Id,StringComparer.Ordinal).FirstOrDefault();var success=outcomes.Where(o=>o.Attempt.Status=="Succeeded").OrderByDescending(o=>o.Delivery.ArchiveWatermark).ThenByDescending(o=>o.Attempt.FinishedAtUtc,StringComparer.Ordinal).FirstOrDefault();var health=new GoogleArchiveHealth("Never completed",watermark,latest,success);
        if(!policy.DriveArchivesEnabled)return health with{Status="Disabled"};var connection=GoogleConnectionRecords.Read(db,business,store.InstanceId);
        try{if(connection is null)throw new GoogleConnectionFailure("GoogleConsentUnavailable","Google connection permission is unavailable.");_settings.Require(db,new(policy,connection,GoogleOutputKind.DriveArchive));}
        catch(GoogleConnectionFailure error){return health with{Status="Permission required",FailureCode=error.Code};}
        if(profile is null || db.Scalar("SELECT Id FROM Owners WHERE Id=$owner AND BusinessId=$business AND Active=1;",("$owner",profile.ConfirmedBy),("$business",business)) is null)return health with{Status="Recovery setup required"};
        if(outcomes.Any(o=>o.Attempt.Status=="Running" && GoogleArchiveDeliveryWorker.IsActive(o.Attempt.Id)))return health with{Status="Running"};
        if(outcomes.Any(o=>o.Attempt.Status=="Running") || latest?.Attempt.Status=="Interrupted")return health with{Status="Needs review"};
        if(latest?.Attempt.Status is "Failed" or "Cancelled")return health with{Status=latest.Attempt.Status,FailureCode=latest.Attempt.FailureCode};
        return health with{Status=success is null?"Never completed":success.Delivery.ArchiveWatermark<watermark?"Pending":"Succeeded"};
    }
}
