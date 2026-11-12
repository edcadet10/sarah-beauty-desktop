using System.Text.Json;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed record GoogleArchiveQueueState(int Version,string BusinessId,string InstanceId,int PolicyRevision,string ConnectionId,string FolderBinding,long ObservedWatermark,long? DeliveredWatermark,string Status,int FailureCount,string? NextAttemptAtUtc,string? LocalArchiveJobId,string? FailureCode);
public sealed record GoogleArchiveQueuePoll(string Status,GoogleArchiveQueueState? State,GoogleArchiveAttempt? Attempt=null);
internal static class GoogleArchiveQueueRecords
{
    private static string Key(string business,string instance)=>"GoogleArchiveQueue/"+business+"/"+instance;
    internal static GoogleArchiveQueueState? Read(DbWork db,string business,string instance)
    {
        string? json=db.Scalar("SELECT Value FROM AppMetadata WHERE Key=$key;",("$key",Key(business,instance))) as string;if(json is null)return null;var value=JsonSerializer.Deserialize<GoogleArchiveQueueState>(json)??throw Invalid();
        if(value.Version!=1 || value.BusinessId!=business || value.InstanceId!=instance || value.PolicyRevision<1 || !Guid.TryParse(value.ConnectionId,out _) || !Guid.TryParseExact(value.FolderBinding,"N",out _) || value.ObservedWatermark<0 || value.DeliveredWatermark<0 || value.FailureCount is <0 or >20 || value.Status is not("Waiting for local archive" or "Pending" or "Running" or "Succeeded" or "Failed" or "Cancelled") || value.NextAttemptAtUtc is not null && !DateTimeOffset.TryParse(value.NextAttemptAtUtc,out _) || value.LocalArchiveJobId is not null && !Guid.TryParse(value.LocalArchiveJobId,out _) || value.FailureCode is not null && !GoogleDeliveryFailures.Known(value.FailureCode))throw Invalid();return value;
    }
    internal static GoogleArchiveQueueState Save(DbWork db,GoogleArchiveQueueState value)
    {
        db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$json) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value;",("$key",Key(value.BusinessId,value.InstanceId)),("$json",JsonSerializer.Serialize(value)));return value;
    }
    private static InvalidDataException Invalid()=>new("The saved Google archive retry state needs review.");
}
