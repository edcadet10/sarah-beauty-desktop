using System.Security.Cryptography;
using System.Text.Json;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed record GoogleArchiveDelivery(int Version,string Id,string BusinessId,string InstanceId,string ConnectionId,string FolderId,string FolderBinding,string LocalArchiveJobId,string LocalArchiveFingerprint,string DriveFileId,long ArchiveWatermark,long ByteLength,string Sha256,string RecipientFingerprint,string CreatedAtUtc);
public sealed record GoogleArchiveAttempt(int Version,string Id,string DeliveryId,string BusinessId,string InstanceId,int PolicyRevision,string Status,string StartedAtUtc,string? FinishedAtUtc=null,string? FailureCode=null);
public sealed record GoogleArchiveDeliveryHistory(GoogleArchiveDelivery Delivery,GoogleArchiveAttempt[] Attempts);
internal static class GoogleArchiveDeliveryRecords
{
    private const string DeliveryPrefix="GoogleArchiveDelivery/",AttemptPrefix="GoogleArchiveAttempt/";
    internal static string Fingerprint(LocalBackupJob source)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(source)));
    internal static GoogleArchiveUploadSpec Spec(GoogleArchiveDelivery value)=>new(value.DriveFileId,value.FolderId,value.FolderBinding,value.Id,value.ByteLength,value.Sha256);
    internal static List<GoogleArchiveDelivery> Deliveries(DbWork db,string business)=>db.Read("SELECT Key,Value FROM AppMetadata WHERE Key GLOB 'GoogleArchiveDelivery/*';",r=>(Key:r.GetString(0),Json:r.GetString(1))).Select(row=>
    {
        var value=JsonSerializer.Deserialize<GoogleArchiveDelivery>(row.Json)??throw Invalid();
        if(value.Version!=1 || value.BusinessId!=business || row.Key!=DeliveryPrefix+value.Id || !Guid.TryParseExact(value.InstanceId,"N",out _) || !Guid.TryParse(value.ConnectionId,out _) || !Guid.TryParse(value.LocalArchiveJobId,out _) || value.ArchiveWatermark<0 || !Hash(value.LocalArchiveFingerprint) || !Hash(value.RecipientFingerprint) || !DateTimeOffset.TryParse(value.CreatedAtUtc,out _))throw Invalid();Spec(value).Validate();return value;
    }).ToList();
    internal static List<GoogleArchiveAttempt> Attempts(DbWork db,string business)=>db.Read("SELECT Key,Value FROM AppMetadata WHERE Key GLOB 'GoogleArchiveAttempt/*';",r=>(Key:r.GetString(0),Json:r.GetString(1))).Select(row=>
    {
        var value=JsonSerializer.Deserialize<GoogleArchiveAttempt>(row.Json)??throw Invalid();
        if(value.Version!=1 || value.BusinessId!=business || row.Key!=AttemptPrefix+value.Id || !Guid.TryParseExact(value.Id,"N",out _) || !Guid.TryParseExact(value.DeliveryId,"N",out _) || !Guid.TryParseExact(value.InstanceId,"N",out _) || value.PolicyRevision<1 || value.Status is not("Running" or "Succeeded" or "Failed" or "Cancelled" or "Interrupted") || !DateTimeOffset.TryParse(value.StartedAtUtc,out _))throw Invalid();
        if(value.Status=="Running"?(value.FinishedAtUtc is not null || value.FailureCode is not null):!DateTimeOffset.TryParse(value.FinishedAtUtc,out _) || (value.Status=="Succeeded"?value.FailureCode is not null:!GoogleDeliveryFailures.Known(value.FailureCode)))throw Invalid();return value;
    }).ToList();
    internal static void Insert(DbWork db,GoogleArchiveDelivery value)=>Insert(db,DeliveryPrefix+value.Id,value);
    internal static void Insert(DbWork db,GoogleArchiveAttempt value)=>Insert(db,AttemptPrefix+value.Id,value);
    internal static void Update(DbWork db,GoogleArchiveAttempt value)=>db.Execute("UPDATE AppMetadata SET Value=$json WHERE Key=$key;",("$json",JsonSerializer.Serialize(value)),("$key",AttemptPrefix+value.Id));
    private static void Insert<T>(DbWork db,string key,T value)=>db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$json);",("$key",key),("$json",JsonSerializer.Serialize(value)));
    private static bool Hash(string? value)=>value is{Length:64} && value.All(Uri.IsHexDigit);
    private static InvalidDataException Invalid()=>new("The saved Google archive delivery history needs review.");
}
internal static class GoogleDeliveryFailures
{
    private static readonly HashSet<string> Codes=["Cancelled","CompletionUnconfirmed","LocalArchiveUnavailable","ArchiveVerificationFailed","AccessUnavailable","OperationFailed","GoogleOperationFailed","GoogleUnavailable","GoogleRequestTimedOut","GoogleQuotaLimited","GoogleStorageFull","GrantUnavailable","ClientUnavailable","UnexpectedScope","GoogleAccountChanged","IncompleteGrant","ConsentDenied","DriveAccessUnavailable","GoogleRequestRejected","GoogleFileUnavailable","GoogleFileAlreadyExists","GoogleArchiveUnconfirmed","GoogleArchiveNotPrivate","GoogleFolderNotPrivate","GoogleConsentUnavailable","GoogleOutputPermissionUnavailable","GoogleOutputConnectionChanged","GoogleResponseTooLarge","InvalidGoogleResponse","GoogleUploadLocationRejected","ConnectionStateUnconfirmed"];
    internal static bool Known(string? code)=>code is not null && Codes.Contains(code);
    static GoogleDeliveryFailures(){Codes.UnionWith(["GoogleArchiveBusy","GoogleArchiveSourceChanged","GoogleArchiveSourceUnavailable"]);}
    internal static string Code(Exception error)=>error switch{OperationCanceledException=>"Cancelled",GoogleConnectionFailure failure=>Known(failure.Code)?failure.Code:"GoogleOperationFailed",InvalidDataException=>"ArchiveVerificationFailed",IOException=>"LocalArchiveUnavailable",UnauthorizedAccessException=>"AccessUnavailable",_=>"OperationFailed"};
}
