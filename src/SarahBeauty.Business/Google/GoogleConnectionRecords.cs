using System.Text.Json;
using System.Text.Json.Serialization;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed record GoogleConnectionRecord(int Version,string Id,string BusinessId,string InstanceId,string ConfigurationId,string Status,string ConnectedBy,string ConnectedAtUtc,GoogleAccountDetails Account,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]string? PreviousConnectionId=null,[property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]string? DisconnectedBy=null,[property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]string? DisconnectedAtUtc=null);
public sealed record GoogleConnectionHealth(string Status,GoogleConnectionRecord? LastConnection,string? FailureCode=null);
internal static class GoogleConnectionRecords
{
    private static string Key(string business,string instance)=>"GoogleConnection/"+business+"/"+instance;
    internal static GoogleConnectionRecord? Read(DbWork db,string business,string instance)
    {
        string? json=db.Scalar("SELECT Value FROM AppMetadata WHERE Key=$key;",("$key",Key(business,instance))) as string;if(json is null)return null;
        var value=JsonSerializer.Deserialize<GoogleConnectionRecord>(json)??throw new InvalidDataException("The recorded Google connection is incomplete.");
        if(value.Version!=1 || value.BusinessId!=business || value.InstanceId!=instance || value.Status is not("Connected" or "Disconnected") || !Guid.TryParse(value.Id,out _) || !Guid.TryParse(value.ConfigurationId,out _) || value.Account?.Account is null || string.IsNullOrWhiteSpace(value.Account.Account.PermissionId) || string.IsNullOrWhiteSpace(value.Account.Account.EmailAddress))throw new InvalidDataException("The recorded Google connection needs review.");
        if(value.Status=="Disconnected" && (!Guid.TryParse(value.PreviousConnectionId,out _) || !Guid.TryParse(value.DisconnectedBy,out _) || !DateTimeOffset.TryParse(value.DisconnectedAtUtc,out _)))throw new InvalidDataException("The local disconnection record needs review.");return value;
    }
    internal static void RequireExpected(DbWork db,string business,string instance,string? expected)
    {
        if(Read(db,business,instance)?.Id!=expected)throw new ArgumentException("The Google connection changed. Reload it before connecting.");
    }
    internal static void RequireConsent(DbWork db,GoogleConnectionRecord record)
    {
        if(record.Status!="Connected" || Read(db,record.BusinessId,record.InstanceId)!=record || GoogleClientRecords.Read(db,record.BusinessId,record.InstanceId)?.Id!=record.ConfigurationId || db.Scalar("SELECT Id FROM Owners WHERE Id=$owner AND BusinessId=$business AND Active=1;",("$owner",record.ConnectedBy),("$business",record.BusinessId)) is null)throw new GoogleConnectionFailure("GoogleConsentUnavailable","The saved Google connection permission changed. Review the current connection.");
    }
    internal static void Save(DbWork db,GoogleConnectionRecord value)=>db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$json) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value;",("$key",Key(value.BusinessId,value.InstanceId)),("$json",JsonSerializer.Serialize(value)));
}
