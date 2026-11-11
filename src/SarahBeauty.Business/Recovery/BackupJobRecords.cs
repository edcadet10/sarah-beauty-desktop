using System.Text.Json;
using System.Text.Json.Serialization;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

public sealed record LocalBackupJob(int Version,string Id,string BusinessId,string Status,string Destination,string RecipientFingerprint,long RequestedWatermark,
    string StartedAtUtc,string? FinishedAtUtc=null,CreatedArchive? Result=null,string? FailureCode=null,[property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]string? InstanceId=null);
public sealed record LocalBackupHealth(string Status,long CurrentWatermark,long? ArchivedWatermark,string? CompletedAtUtc,LocalBackupJob? LastAttempt,LocalBackupJob? LastSuccess);
internal static class BackupJobRecords
{
    private const string Prefix="LocalArchiveJob/";
    internal static string Key(string id)=>Prefix+id;
    internal static LocalBackupJob Read(string json,string businessId,string key)
    {
        var job=JsonSerializer.Deserialize<LocalBackupJob>(json)??throw new InvalidDataException("The archive history record is incomplete.");
        if(job.Version!=1 || job.BusinessId!=businessId || key!=Key(job.Id) || !Guid.TryParse(job.Id,out _) || !ArchiveManifestChecks.Hash(job.RecipientFingerprint) || job.RequestedWatermark<0 || job.Status is not("Running" or "Succeeded" or "Failed" or "Cancelled"))throw new InvalidDataException("The archive history record needs review.");
        if(job.InstanceId is not null && (!Guid.TryParseExact(job.InstanceId,"N",out var instance)||instance==Guid.Empty))throw new InvalidDataException("The archive source instance is invalid.");
        if(job.Status=="Succeeded" && (job.Result is null || job.Result.Manifest.BusinessId!=businessId || job.Result.Manifest.Watermark<job.RequestedWatermark || job.Result.RecipientFingerprint!=job.RecipientFingerprint || job.Result.ByteLength<1 || !ArchiveManifestChecks.Hash(job.Result.Sha256) || !ArchiveManifestChecks.Hash(job.Result.ManifestHash)))throw new InvalidDataException("The completed archive facts need review.");return job;
    }
    internal static LocalBackupJob Get(DbWork db,string businessId,string id)
    {
        string key=Key(id);string json=db.Scalar("SELECT Value FROM AppMetadata WHERE Key=$key;",("$key",key)) as string??throw new ArgumentException("Choose a saved local archive attempt.");return Read(json,businessId,key);
    }
    internal static List<LocalBackupJob> List(DbWork db,string businessId)=>db.Read("SELECT Key,Value FROM AppMetadata WHERE Key GLOB 'LocalArchiveJob/*';",r=>(Key:r.GetString(0),Json:r.GetString(1)))
        .Select(row=>Read(row.Json,businessId,row.Key)).OrderByDescending(j=>j.StartedAtUtc,StringComparer.Ordinal).ThenByDescending(j=>j.Id,StringComparer.Ordinal).ToList();
    internal static void Insert(DbWork db,LocalBackupJob job)=>db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$json);",("$key",Key(job.Id)),("$json",JsonSerializer.Serialize(job)));
    internal static void Complete(DbWork db,LocalBackupJob completed)
    {
        string key=Key(completed.Id);string json=(string?)db.Scalar("SELECT Value FROM AppMetadata WHERE Key=$key;",("$key",key))??throw new InvalidDataException("The running archive record is unavailable.");
        var current=Read(json,completed.BusinessId,key);if(current.Status!="Running" || current.RecipientFingerprint!=completed.RecipientFingerprint || current.Destination!=completed.Destination || current.InstanceId!=completed.InstanceId)throw new InvalidDataException("The archive job changed before completion.");
        db.Execute("UPDATE AppMetadata SET Value=$json WHERE Key=$key;",("$json",JsonSerializer.Serialize(completed)),("$key",key));
    }
}
