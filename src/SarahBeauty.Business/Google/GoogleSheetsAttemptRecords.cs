using System.Globalization;
using System.Text.Json;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed record GoogleSheetsAttempt(int Version,string Id,string JobId,string BusinessId,string InstanceId,int PolicyRevision,string Status,string StartedAtUtc,
    string? FinishedAtUtc=null,string? FailureCode=null,string? FileId=null,string? VerifiedAtUtc=null);
public sealed record GoogleSheetsDeliveryHistory(GoogleSheetsExportJob Job,GoogleSheetsCreation? Creation,GoogleSheetsAttempt[] Attempts);
internal sealed record GoogleSheetsVerifiedDelivery(string FileId,string GenerationId,string ManifestSha256,long SourceWatermark,string VerifiedAtUtc);

internal static class GoogleSheetsAttemptRecords
{
    private const string Prefix="GoogleSheetsAttempt/";
    internal static IReadOnlyList<GoogleSheetsAttempt> List(DbWork db,string business)=>db.Read("SELECT Key,Value FROM AppMetadata WHERE Key GLOB $prefix ORDER BY Key;",
        r=>(Key:r.GetString(0),Json:r.GetString(1)),("$prefix",Prefix+business+"/*")).Select(row=>Read(row.Key,row.Json,business)).ToArray();
    internal static void Insert(DbWork db,GoogleSheetsAttempt value)
    {
        Validate(value);db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$value);",("$key",Key(value)),("$value",JsonSerializer.Serialize(value)));
    }
    internal static void Replace(DbWork db,GoogleSheetsAttempt expected,GoogleSheetsAttempt next)
    {
        Validate(next);if(List(db,expected.BusinessId).SingleOrDefault(value=>value.Id==expected.Id)!=expected)throw Invalid();
        if(db.Execute("UPDATE AppMetadata SET Value=$value WHERE Key=$key;",("$key",Key(expected)),("$value",JsonSerializer.Serialize(next)))!=1)throw Invalid();
    }
    private static GoogleSheetsAttempt Read(string key,string json,string business)
    {
        try{var value=JsonSerializer.Deserialize<GoogleSheetsAttempt>(json)??throw Invalid();Validate(value);if(value.BusinessId!=business||key!=Key(value))throw Invalid();return value;}
        catch(JsonException){throw Invalid();}
    }
    private static void Validate(GoogleSheetsAttempt value)
    {
        if(value.Version!=1||!Id(value.Id)||!Id(value.JobId)||!Id(value.InstanceId)||!Guid.TryParse(value.BusinessId,out var business)||business==Guid.Empty||value.PolicyRevision<1||!Utc(value.StartedAtUtc)||
            value.Status is not("Running" or "Succeeded" or "Failed" or "Cancelled" or "Interrupted"))throw Invalid();
        if(value.Status=="Running"?(value.FinishedAtUtc is not null||value.FailureCode is not null||value.FileId is not null||value.VerifiedAtUtc is not null):
            !Utc(value.FinishedAtUtc)||(value.Status=="Succeeded"?(value.FailureCode is not null||value.FileId is null||!Utc(value.VerifiedAtUtc)):
            !GoogleSheetsDeliveryFailures.Known(value.FailureCode)||value.FileId is not null||value.VerifiedAtUtc is not null))throw Invalid();
        if(value.FileId is not null){try{GoogleDriveFolders.RequireId(value.FileId);}catch(GoogleConnectionFailure){throw Invalid();}}
    }
    internal static bool Utc(string? value)=>DateTimeOffset.TryParseExact(value,"O",CultureInfo.InvariantCulture,DateTimeStyles.None,out var time)&&time.Offset==TimeSpan.Zero;
    private static bool Id(string value)=>Guid.TryParseExact(value,"N",out var id)&&id!=Guid.Empty;
    private static string Key(GoogleSheetsAttempt value)=>Prefix+value.BusinessId+"/"+value.Id;
    internal static InvalidDataException Invalid()=>new("The saved readable delivery attempt changed or needs review.");
}

internal static class GoogleSheetsDeliveryFailures
{
    private static readonly HashSet<string> Codes=["GoogleSheetsBusy","GoogleSheetsSourceChanged","GoogleSheetsAlreadyVerified","ReadableVerificationFailed","ReadableSourceUnavailable",
        "ReadableWorkbookCreationUnconfirmed","ReadableWorkbookLookupUnconfirmed","ReadableWorkbookNotPrivateOrBound","ReadableGridMismatch","ReadableCellMismatch",
        "ReadableInitialSchemaUnsupported","ReadableWriteUnconfirmed","ReadableReadLocationRejected","ReadableSchemaSourceMismatch","ReadableSchemaUnconfirmed","ReadableSchemaMismatch","ReadablePublicationUnconfirmed"];
    internal static bool Known(string? code)=>code is not null&&(Codes.Contains(code)||GoogleDeliveryFailures.Known(code));
    internal static string Code(Exception error)=>error switch{GoogleConnectionFailure failure=>Known(failure.Code)?failure.Code:"GoogleOperationFailed",InvalidDataException=>"ReadableVerificationFailed",IOException=>"ReadableSourceUnavailable",_=>GoogleDeliveryFailures.Code(error)};
}
