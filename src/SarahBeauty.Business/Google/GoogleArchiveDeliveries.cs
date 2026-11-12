using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed class GoogleArchiveDeliveries(BusinessStore store)
{
    public IReadOnlyList<GoogleArchiveDeliveryHistory> History(OwnerSession owner)=>store.ReadSnapshot(db=>
    {
        store.RequireSession(db,owner);var attempts=GoogleArchiveDeliveryRecords.Attempts(db,owner.BusinessId);return GoogleArchiveDeliveryRecords.Deliveries(db,owner.BusinessId).OrderByDescending(d=>d.CreatedAtUtc,StringComparer.Ordinal).ThenBy(d=>d.Id,StringComparer.Ordinal).Select(delivery=>new GoogleArchiveDeliveryHistory(delivery,attempts.Where(a=>a.DeliveryId==delivery.Id).OrderBy(a=>a.StartedAtUtc,StringComparer.Ordinal).ThenBy(a=>a.Id,StringComparer.Ordinal).ToArray())).ToArray();
    });
    internal GoogleArchiveDelivery Reserve(GoogleOutputPermit permit,string localArchiveJobId,string driveFileId)=>store.Transaction(db=>
    {
        GoogleDriveFolders.RequireId(driveFileId);RequirePermission(db,permit);var source=Source(db,permit,localArchiveJobId);string fingerprint=GoogleArchiveDeliveryRecords.Fingerprint(source);var policy=permit.Policy;
        var prior=GoogleArchiveDeliveryRecords.Deliveries(db,policy.BusinessId).SingleOrDefault(d=>d.InstanceId==store.InstanceId && d.ConnectionId==policy.ConnectionId && d.FolderId==policy.FolderId && d.FolderBinding==policy.FolderBinding && d.LocalArchiveJobId==source.Id && d.LocalArchiveFingerprint==fingerprint);if(prior is not null)return prior;
        var value=new GoogleArchiveDelivery(1,Guid.NewGuid().ToString("N"),policy.BusinessId,store.InstanceId,policy.ConnectionId,policy.FolderId,policy.FolderBinding,source.Id,fingerprint,driveFileId,source.Result!.Manifest.Watermark,source.Result.ByteLength,source.Result.Sha256,source.RecipientFingerprint,DateTimeOffset.UtcNow.ToString("O"));GoogleArchiveDeliveryRecords.Insert(db,value);return value;
    });
    internal GoogleArchiveAttempt Begin(GoogleOutputPermit permit,GoogleArchiveDelivery delivery)=>store.Transaction(db=>
    {
        Require(db,permit,delivery);if(GoogleArchiveDeliveryRecords.Attempts(db,delivery.BusinessId).Any(a=>a.DeliveryId==delivery.Id && a.Status=="Running"))throw new GoogleConnectionFailure("GoogleArchiveBusy","A prior archive attempt is still running or needs interrupted-state review.");
        var attempt=new GoogleArchiveAttempt(1,Guid.NewGuid().ToString("N"),delivery.Id,delivery.BusinessId,store.InstanceId,permit.Policy.Revision,"Running",DateTimeOffset.UtcNow.ToString("O"));GoogleArchiveDeliveryRecords.Insert(db,attempt);return attempt;
    });
    internal GoogleArchiveAttempt Complete(GoogleOutputPermit permit,GoogleArchiveDelivery delivery,GoogleArchiveAttempt attempt,GoogleUploadedArchive result)=>store.Transaction(db=>
    {
        Require(db,permit,delivery);if(attempt.DeliveryId!=delivery.Id || attempt.PolicyRevision!=permit.Policy.Revision || result.FileId!=delivery.DriveFileId || result.ByteLength!=delivery.ByteLength || result.Sha256!=delivery.Sha256)throw GoogleArchiveUploadSpec.Unconfirmed();return Finish(db,attempt,"Succeeded",null);
    });
    internal GoogleArchiveAttempt Fail(GoogleArchiveAttempt attempt,Exception error)=>store.Transaction(db=>Finish(db,attempt,error is OperationCanceledException?"Cancelled":"Failed",GoogleDeliveryFailures.Code(error)));
    internal void InterruptRunning(GoogleArchiveDelivery delivery)=>store.Transaction(db=>
    {
        if(delivery.InstanceId!=store.InstanceId || GoogleArchiveDeliveryRecords.Deliveries(db,delivery.BusinessId).SingleOrDefault(d=>d.Id==delivery.Id)!=delivery)throw new InvalidDataException("The Google archive delivery changed.");
        foreach(var attempt in GoogleArchiveDeliveryRecords.Attempts(db,delivery.BusinessId).Where(a=>a.DeliveryId==delivery.Id && a.Status=="Running"))Finish(db,attempt,"Interrupted","CompletionUnconfirmed");return true;
    });
    internal void Require(DbWork db,GoogleOutputPermit permit,GoogleArchiveDelivery delivery)
    {
        RequirePermission(db,permit);var policy=permit.Policy;
        if(delivery.InstanceId!=store.InstanceId || delivery.BusinessId!=policy.BusinessId || delivery.ConnectionId!=policy.ConnectionId || delivery.FolderId!=policy.FolderId || delivery.FolderBinding!=policy.FolderBinding || GoogleArchiveDeliveryRecords.Deliveries(db,policy.BusinessId).SingleOrDefault(d=>d.Id==delivery.Id)!=delivery || GoogleArchiveDeliveryRecords.Fingerprint(Source(db,permit,delivery.LocalArchiveJobId))!=delivery.LocalArchiveFingerprint)throw new GoogleConnectionFailure("GoogleArchiveSourceChanged","The saved archive source or Google destination changed. Review this delivery before retrying.");
    }
    private void RequirePermission(DbWork db,GoogleOutputPermit permit)
    {
        if(permit.Kind!=GoogleOutputKind.DriveArchive)throw new UnauthorizedAccessException("Drive archive permission is required.");new GoogleOutputSettings(store).Require(db,permit);
    }
    private LocalBackupJob Source(DbWork db,GoogleOutputPermit permit,string id)
    {
        var source=BackupJobRecords.Get(db,permit.Policy.BusinessId,id);var profile=RecoveryProfiles.Read(db,permit.Policy.BusinessId);
        if(source.InstanceId!=store.InstanceId || source.Status!="Succeeded" || source.Result is null || source.Result.Manifest.StoreId!=store.StoreId || profile is null || source.RecipientFingerprint!=profile.RecipientFingerprint || db.Scalar("SELECT Id FROM Owners WHERE Id=$owner AND BusinessId=$business AND Active=1;",("$owner",profile.ConfirmedBy),("$business",permit.Policy.BusinessId)) is null)throw new GoogleConnectionFailure("GoogleArchiveSourceUnavailable","Choose a completed archive from this writable instance and its current confirmed recovery material.");return source;
    }
    private GoogleArchiveAttempt Finish(DbWork db,GoogleArchiveAttempt original,string status,string? code)
    {
        var current=GoogleArchiveDeliveryRecords.Attempts(db,original.BusinessId).SingleOrDefault(a=>a.Id==original.Id);if(original.InstanceId!=store.InstanceId || current!=original || current.Status!="Running")throw new InvalidDataException("The running Google archive attempt changed before completion.");var completed=original with{Status=status,FinishedAtUtc=DateTimeOffset.UtcNow.ToString("O"),FailureCode=code};GoogleArchiveDeliveryRecords.Update(db,completed);return completed;
    }
}
