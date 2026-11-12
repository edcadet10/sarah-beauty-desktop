using System.Globalization;
using System.Text.Json;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed record GoogleSheetsCreation(int Version,string JobId,string BusinessId,string InstanceId,int Revision,string State,string? FileId,string UpdatedAtUtc);

internal static class GoogleSheetsCreationRecords
{
    internal static GoogleSheetsCreation? Get(DbWork db,GoogleSheetsExportJob job)
    {
        string key=Key(job);string? json=db.Scalar("SELECT Value FROM AppMetadata WHERE Key=$key;",("$key",key)) as string;if(json is null)return null;
        try
        {
            var value=JsonSerializer.Deserialize<GoogleSheetsCreation>(json)??throw Invalid();Validate(value,job);return value;
        }
        catch(JsonException){throw Invalid();}
    }
    internal static void Insert(DbWork db,GoogleSheetsExportJob job,GoogleSheetsCreation value)
    {
        Validate(value,job);db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$value);",("$key",Key(job)),("$value",JsonSerializer.Serialize(value)));
    }
    internal static void Replace(DbWork db,GoogleSheetsExportJob job,GoogleSheetsCreation expected,GoogleSheetsCreation next)
    {
        Validate(next,job);if(Get(db,job)!=expected)throw Invalid();
        if(db.Execute("UPDATE AppMetadata SET Value=$value WHERE Key=$key;",("$key",Key(job)),("$value",JsonSerializer.Serialize(next)))!=1)throw Invalid();
    }
    private static void Validate(GoogleSheetsCreation value,GoogleSheetsExportJob job)
    {
        if(value.Version!=1||value.JobId!=job.Id||value.BusinessId!=job.BusinessId||value.InstanceId!=job.InstanceId||
            !DateTimeOffset.TryParseExact(value.UpdatedAtUtc,"O",CultureInfo.InvariantCulture,DateTimeStyles.None,out var time)||time.Offset!=TimeSpan.Zero||
            value.State switch{"Prepared"=>value.Revision!=1||value.FileId is not null,"CreateUncertain"=>value.Revision!=2||value.FileId is not null,"FileKnown"=>value.Revision!=3||value.FileId is null,_=>true})throw Invalid();
        if(value.FileId is not null){try{GoogleDriveFolders.RequireId(value.FileId);}catch(GoogleConnectionFailure){throw Invalid();}}
    }
    private static string Key(GoogleSheetsExportJob job)=>"GoogleSheetsCreation/"+job.BusinessId+"/"+job.Id;
    internal static InvalidDataException Invalid()=>new("The saved workbook creation checkpoint changed or needs review. Do not create another workbook for this job.");
}
