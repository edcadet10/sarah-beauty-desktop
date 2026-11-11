using System.Text.Json;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

public sealed record BackupQueueState(int Version,string BusinessId,string InstanceId,int ScheduleRevision,string RecipientFingerprint,
    long PendingWatermark,long? ArchivedWatermark,string Status,int FailureCount,string? NextAttemptAtUtc,string? LastJobId,string? FailureCode);
public sealed record AutomaticBackupPoll(string Status,BackupQueueState? State,LocalBackupJob? CompletedJob=null);
internal static class BackupQueueRecords
{
    private static string Key(string business,string instance)=>"LocalArchiveQueue/"+business+"/"+instance;
    internal static BackupQueueState? Read(DbWork db,string business,string instance)
    {
        string? json=db.Scalar("SELECT Value FROM AppMetadata WHERE Key=$key;",("$key",Key(business,instance))) as string;if(json is null)return null;
        var value=JsonSerializer.Deserialize<BackupQueueState>(json)??throw new InvalidDataException("The automatic archive queue is incomplete.");
        if(value.Version!=1 || value.BusinessId!=business || value.InstanceId!=instance || value.ScheduleRevision<1 || !ArchiveManifestChecks.Hash(value.RecipientFingerprint) || value.PendingWatermark<0 || value.ArchivedWatermark<0 || value.FailureCount is <0 or >20 || value.Status is not("Pending" or "Running" or "Succeeded" or "Failed" or "Cancelled"))throw new InvalidDataException("The automatic archive queue needs review.");
        if(value.NextAttemptAtUtc is not null && !DateTimeOffset.TryParse(value.NextAttemptAtUtc,out _))throw new InvalidDataException("The automatic archive retry time needs review.");return value;
    }
    internal static BackupQueueState Save(DbWork db,BackupQueueState state)
    {
        if(Read(db,state.BusinessId,state.InstanceId)==state)return state;
        db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$json) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value;",("$key",Key(state.BusinessId,state.InstanceId)),("$json",JsonSerializer.Serialize(state)));return state;
    }
}
