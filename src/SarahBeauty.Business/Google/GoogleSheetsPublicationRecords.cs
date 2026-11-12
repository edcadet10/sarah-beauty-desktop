using System.Text.Json;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed record GoogleSheetsPublication(int Version,string JobId,string BusinessId,string InstanceId,string FileId,string GenerationId,string ManifestSha256,long SourceWatermark,string SourceReadbackAtUtc);

internal static class GoogleSheetsPublicationRecords
{
    internal static GoogleSheetsPublication? Get(DbWork db,GoogleSheetsExportJob job)
    {
        string? json=db.Scalar("SELECT Value FROM AppMetadata WHERE Key=$key;",("$key",Key(job))) as string;if(json is null)return null;
        try{var value=JsonSerializer.Deserialize<GoogleSheetsPublication>(json)??throw Invalid();Require(value,job);return value;}catch(JsonException){throw Invalid();}
    }
    internal static void Insert(DbWork db,GoogleSheetsExportJob job,GoogleSheetsPublication value)
    {
        Require(value,job);db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$value);",("$key",Key(job)),("$value",JsonSerializer.Serialize(value)));
    }
    internal static void Require(GoogleSheetsPublication value,GoogleSheetsExportJob job)
    {
        if(value.Version!=1||value.JobId!=job.Id||value.BusinessId!=job.BusinessId||value.InstanceId!=job.InstanceId||value.GenerationId!=job.GenerationId||value.ManifestSha256!=job.ManifestSha256||value.SourceWatermark!=job.SourceWatermark||!GoogleSheetsAttemptRecords.Utc(value.SourceReadbackAtUtc))throw Invalid();
        try{GoogleDriveFolders.RequireId(value.FileId);}catch(GoogleConnectionFailure){throw Invalid();}
    }
    private static string Key(GoogleSheetsExportJob job)=>"GoogleSheetsPublication/"+job.BusinessId+"/"+job.Id;
    internal static InvalidDataException Invalid()=>new("The saved readable publication checkpoint does not match this job and file. Existing workbooks are preserved.");
}

internal sealed class GoogleSheetsPublicationCheckpoints(BusinessStore store)
{
    // The worker calls this only after independently rereading every source cell.
    internal GoogleSheetsPublication Prepare(GoogleOutputPermit permit,GoogleSheetsExportJob job,GoogleSheetsAttempt attempt,CancellationToken cancellation=default)=>store.Transaction(db=>
    {
        cancellation.ThrowIfCancellationRequested();new GoogleSheetsDeliveries(store).RequireRunning(db,permit,job,attempt);var creation=GoogleSheetsCreationRecords.Get(db,job);
        if(creation is not{State:"FileKnown"})throw GoogleSheetsPublicationRecords.Invalid();var prior=GoogleSheetsPublicationRecords.Get(db,job);
        if(prior is not null){if(prior.FileId!=creation.FileId)throw GoogleSheetsPublicationRecords.Invalid();return prior;}
        var value=new GoogleSheetsPublication(1,job.Id,job.BusinessId,job.InstanceId,creation.FileId!,job.GenerationId,job.ManifestSha256,job.SourceWatermark,DateTimeOffset.UtcNow.ToString("O"));
        GoogleSheetsPublicationRecords.Insert(db,job,value);cancellation.ThrowIfCancellationRequested();return value;
    });
}
