using System.Text.Json;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

internal sealed record GoogleConnectionIssue(int Version,string ConnectionId,string BusinessId,string InstanceId,string Code,string ObservedAtUtc);
internal static class GoogleConnectionIssues
{
    private static string Key(string business,string instance)=>"GoogleConnectionIssue/"+business+"/"+instance;
    internal static bool RequiresConsent(string code)=>code is "GrantUnavailable" or "ClientUnavailable" or "UnexpectedScope" or "GoogleAccountChanged" or "IncompleteGrant" or "ConsentDenied";
    internal static GoogleConnectionIssue? Read(DbWork db,string business,string instance)
    {
        string? json=db.Scalar("SELECT Value FROM AppMetadata WHERE Key=$key;",("$key",Key(business,instance))) as string;if(json is null)return null;
        var value=JsonSerializer.Deserialize<GoogleConnectionIssue>(json)??throw new InvalidDataException("The Google connection issue is incomplete.");
        if(value.Version!=1 || value.BusinessId!=business || value.InstanceId!=instance || !Guid.TryParse(value.ConnectionId,out _) || !RequiresConsent(value.Code) || !DateTimeOffset.TryParse(value.ObservedAtUtc,out _))throw new InvalidDataException("The Google connection issue needs review.");return value;
    }
    internal static void RequireClear(DbWork db,GoogleConnectionRecord record)
    {
        var issue=Read(db,record.BusinessId,record.InstanceId);if(issue?.ConnectionId==record.Id)throw new GoogleConnectionFailure(issue.Code,"The saved Google authorization needs renewed consent. Reconnect Google.");
    }
    internal static void Save(DbWork db,GoogleConnectionIssue value)=>db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$json) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value;",("$key",Key(value.BusinessId,value.InstanceId)),("$json",JsonSerializer.Serialize(value)));
    internal static void Clear(DbWork db,string business,string instance)=>db.Execute("DELETE FROM AppMetadata WHERE Key=$key;",("$key",Key(business,instance)));
}
