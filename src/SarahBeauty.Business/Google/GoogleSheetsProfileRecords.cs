using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed record GoogleSheetsExportProfile(int Version,string BusinessId,string InstanceId,int Revision,ReadableExportSelection Selection,bool AutomaticEnabled,int MinimumIntervalMinutes,string ConfirmedBy,string ConfirmedAtUtc);
public sealed record GoogleSheetsProfileDraft(ReadableExportSelection Selection,bool AutomaticEnabled,int MinimumIntervalMinutes,int ExpectedRevision);

internal static class GoogleSheetsProfileRecords
{
    internal static GoogleSheetsExportProfile? Read(DbWork db,string business,string instance)
    {
        string? json=db.Scalar("SELECT Value FROM AppMetadata WHERE Key=$key;",("$key",Key(business,instance))) as string;if(json is null)return null;
        try
        {
            var value=JsonSerializer.Deserialize<GoogleSheetsExportProfile>(json)??throw Invalid();Validate(value);if(value.BusinessId!=business||value.InstanceId!=instance)throw Invalid();return value;
        }
        catch(JsonException){throw Invalid();}
    }
    internal static void Save(DbWork db,GoogleSheetsExportProfile value)
    {
        Validate(value);db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$value) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value;",("$key",Key(value.BusinessId,value.InstanceId)),("$value",JsonSerializer.Serialize(value)));
    }
    internal static void ValidateSelection(ReadableExportSelection selection,int minutes)
    {
        ArgumentNullException.ThrowIfNull(selection);selection.Validate();if(minutes is <1 or >10080)throw new ArgumentException("Choose a readable export interval from 1 to 10,080 minutes.");
    }
    private static void Validate(GoogleSheetsExportProfile value)
    {
        if(value.Version!=1||!Guid.TryParse(value.BusinessId,out var business)||business==Guid.Empty||!Guid.TryParseExact(value.InstanceId,"N",out var instance)||instance==Guid.Empty||value.Revision<1||!Guid.TryParse(value.ConfirmedBy,out var owner)||owner==Guid.Empty||!GoogleSheetsAttemptRecords.Utc(value.ConfirmedAtUtc))throw Invalid();
        try{ValidateSelection(value.Selection,value.MinimumIntervalMinutes);}catch(ArgumentException){throw Invalid();}
    }
    private static string Key(string business,string instance)=>"GoogleSheetsProfile/"+business+"/"+instance;
    private static InvalidDataException Invalid()=>new("The saved readable report dates or automatic export settings need review. Existing history is preserved.");
}
