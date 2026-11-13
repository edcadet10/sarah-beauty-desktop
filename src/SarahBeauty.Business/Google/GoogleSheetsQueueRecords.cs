using System.Text.Json;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed record GoogleSheetsQueueState(int Version,string BusinessId,string InstanceId,int ProfileRevision,int PolicyRevision,string ConnectionId,string FolderBinding,
    long ObservedWatermark,long? DeliveredWatermark,string Status,int FailureCount,string? NextAttemptAtUtc,string? JobId,string? FailureCode,string UpdatedAtUtc);
public sealed record GoogleSheetsQueuePoll(string Status,GoogleSheetsQueueState? State,GoogleSheetsAttempt? Attempt=null);

internal static class GoogleSheetsQueueRecords
{
    internal static GoogleSheetsQueueState? Read(DbWork db,string business,string instance)
    {
        string? json=db.Scalar("SELECT Value FROM AppMetadata WHERE Key=$key;",("$key",Key(business,instance))) as string;if(json is null)return null;
        try
        {
            var value=JsonSerializer.Deserialize<GoogleSheetsQueueState>(json)??throw Invalid();Validate(value);if(value.BusinessId!=business||value.InstanceId!=instance)throw Invalid();return value;
        }
        catch(JsonException){throw Invalid();}
    }
    internal static GoogleSheetsQueueState Put(DbWork db,GoogleSheetsQueueState? expected,GoogleSheetsQueueState value)
    {
        Validate(value);if(expected is not null&&(expected.BusinessId!=value.BusinessId||expected.InstanceId!=value.InstanceId))throw Invalid();
        if(Read(db,value.BusinessId,value.InstanceId)!=expected)throw Invalid();
        string sql=expected is null?"INSERT INTO AppMetadata(Key,Value) VALUES($key,$value);":"UPDATE AppMetadata SET Value=$value WHERE Key=$key;";
        if(db.Execute(sql,("$key",Key(value.BusinessId,value.InstanceId)),("$value",JsonSerializer.Serialize(value)))!=1)throw Invalid();return value;
    }
    private static void Validate(GoogleSheetsQueueState value)
    {
        if(value.Version!=1||!Guid.TryParse(value.BusinessId,out var business)||business==Guid.Empty||!NId(value.InstanceId)||!Guid.TryParse(value.ConnectionId,out var connection)||connection==Guid.Empty||!NId(value.FolderBinding)||
            value.ProfileRevision<1||value.PolicyRevision<1||value.ObservedWatermark<0||value.DeliveredWatermark<0||value.DeliveredWatermark>value.ObservedWatermark||value.FailureCount is <0 or >20||!GoogleSheetsAttemptRecords.Utc(value.UpdatedAtUtc)||
            value.Status is not("Preparing" or "Running" or "Pending" or "Succeeded" or "Failed" or "Cancelled" or "Needs review" or "Disabled")||
            value.NextAttemptAtUtc is not null&&!GoogleSheetsAttemptRecords.Utc(value.NextAttemptAtUtc)||value.JobId is not null&&!NId(value.JobId)||value.FailureCode is not null&&!GoogleSheetsDeliveryFailures.Known(value.FailureCode))throw Invalid();
        if(value.Status=="Running"&&value.JobId is null||value.Status=="Preparing"&&value.JobId is not null||value.Status=="Succeeded"&&(value.DeliveredWatermark is null||value.DeliveredWatermark!=value.ObservedWatermark)||
            value.Status is "Failed" or "Cancelled"&&(value.FailureCount<1||value.FailureCode is null||value.NextAttemptAtUtc is null))throw Invalid();
    }
    private static bool NId(string value)=>Guid.TryParseExact(value,"N",out var id)&&id!=Guid.Empty;
    private static string Key(string business,string instance)=>"GoogleSheetsQueue/"+business+"/"+instance;
    internal static InvalidDataException Invalid()=>new("The saved readable export queue state changed or needs review. Existing jobs and files are preserved.");
}
