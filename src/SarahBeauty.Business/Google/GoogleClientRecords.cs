using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed record GoogleClientConfiguration(int Version,string Id,string BusinessId,string InstanceId,int Revision,string ProjectId,string ClientFingerprint,string ConfiguredBy,string ConfiguredAtUtc);
internal static class GoogleClientRecords
{
    private static string Key(string business,string instance)=>"GoogleDesktopClient/"+business+"/"+instance;
    internal static string Fingerprint(GoogleDesktopClient client)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(client.ClientId)));
    internal static GoogleClientConfiguration? Read(DbWork db,string business,string instance)
    {
        string? json=db.Scalar("SELECT Value FROM AppMetadata WHERE Key=$key;",("$key",Key(business,instance))) as string;if(json is null)return null;
        var value=JsonSerializer.Deserialize<GoogleClientConfiguration>(json)??throw new InvalidDataException("The recorded Google client configuration is incomplete.");
        if(value.Version!=1 || value.BusinessId!=business || value.InstanceId!=instance || value.Revision<1 || !Guid.TryParse(value.Id,out _) || value.ClientFingerprint is not{Length:64} || !value.ClientFingerprint.All(Uri.IsHexDigit))throw new InvalidDataException("The recorded Google client configuration needs review.");return value;
    }
    internal static void RequireExpected(DbWork db,string business,string instance,string? expected)
    {
        if(Read(db,business,instance)?.Id!=expected)throw new ArgumentException("The Google client configuration changed. Reload it before saving.");
    }
    internal static void Save(DbWork db,GoogleClientConfiguration value)=>db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$json) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value;",("$key",Key(value.BusinessId,value.InstanceId)),("$json",JsonSerializer.Serialize(value)));
}
internal sealed class GoogleClientPayload
{
    public int Version {get;set;}=1;
    public string ConfigurationId {get;set;}="";
    public byte[] DownloadedJson {get;set;}=[];
    public override string ToString()=>"Protected Google client payload (credentials hidden)";
}
internal sealed record ConfiguredGoogleClient(GoogleClientConfiguration Configuration,GoogleDesktopClient Client);
