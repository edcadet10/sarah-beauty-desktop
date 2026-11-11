using System.Security.Cryptography;
using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

public sealed record StoreRetirementReceipt(int Version,string StoreId,string InstanceId,string OwnerId,string RetiredAtUtc,string Reason,
    string ArchiveJobId,string ArchiveHash,string ManifestHash,long ArchivedWatermark,long RetirementWatermark);
public sealed class StoreRetirement(BusinessStore store)
{
    internal Action<string>? FailurePoint {get;set;}
    public async Task<StoreRetirementReceipt> RetireAsync(OwnerSession owner,string archiveJobId,string reason,string passphrase,CancellationToken cancellation=default)
    {
        if(string.IsNullOrWhiteSpace(reason)||reason.Length>4000)throw new ArgumentException("Enter a retirement reason of at most 4,000 characters.");
        await Task.Run(()=>new OwnerAccess(store).Confirm(owner,passphrase),cancellation);
        var job=store.Read(db=>{store.RequireSession(db,owner);return CoveredArchive(db,owner,archiveJobId);});
        using var input=new FileStream(job.Destination,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(input.Length!=job.Result!.ByteLength || Convert.ToHexString(await SHA256.HashDataAsync(input,cancellation))!=job.Result.Sha256)throw new InvalidDataException("The archive changed or is incomplete. Create and verify a new current archive before retiring this instance.");
        FailurePoint?.Invoke("Archive verified");cancellation.ThrowIfCancellationRequested();
        var receipt=store.Transaction(db=>Record(db,owner,job,reason,cancellation));owner.Lock();return receipt;
    }
    private LocalBackupJob CoveredArchive(DbWork db,OwnerSession owner,string id)
    {
        var job=BackupJobRecords.Get(db,owner.BusinessId,id);long current=(long)db.Scalar("SELECT Watermark FROM Business WHERE Id=$business;",("$business",owner.BusinessId))!;
        if(job.Status!="Succeeded" || job.Result is null || job.InstanceId!=store.InstanceId || job.Result.Manifest.StoreId!=store.StoreId || job.Result.Manifest.Mode!="TEST" || job.Result.Manifest.Watermark!=current)
            throw new ArgumentException("Create a complete archive of this current writable instance and all pending changes before retiring it.");return job;
    }
    private StoreRetirementReceipt Record(DbWork db,OwnerSession owner,LocalBackupJob reviewed,string reason,CancellationToken cancellation)
    {
        store.RequireSession(db,owner);var current=CoveredArchive(db,owner,reviewed.Id);
        if(current.Result!.Sha256!=reviewed.Result!.Sha256 || current.Result.ByteLength!=reviewed.Result.ByteLength || current.Result.ManifestHash!=reviewed.Result.ManifestHash || current.Destination!=reviewed.Destination)throw new InvalidDataException("The selected archive history changed during retirement review.");
        db.Execute("UPDATE AppMetadata SET Value='Retired' WHERE Key='WritableInstanceState';");BusinessStore.Audit(db,owner,"Retired local writable store instance",store.InstanceId);
        var receipt=new StoreRetirementReceipt(1,store.StoreId,store.InstanceId,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"),reason.Trim(),current.Id,current.Result.Sha256,current.Result.ManifestHash,current.Result.Manifest.Watermark,(long)db.Scalar("SELECT Watermark FROM Business WHERE Id=$business;",("$business",owner.BusinessId))!);
        db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$json);",("$key","RetiredStore/"+store.InstanceId),("$json",JsonSerializer.Serialize(receipt)));FailurePoint?.Invoke("Retirement recorded");cancellation.ThrowIfCancellationRequested();return receipt;
    }
}
