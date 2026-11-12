using System.Globalization;
using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed record GoogleSheetsExportJob(int Version,string Id,string BusinessId,string StoreId,string InstanceId,string ConnectionId,string FolderId,string FolderBinding,
    string GenerationId,string ManifestSha256,string Mode,int SchemaVersion,long SourceWatermark,string CapturedAtUtc,ReadableExportSelection Selection,string CreatedAtUtc);

internal static class GoogleSheetsJobRecords
{
    private const string Prefix="GoogleSheetsJob/";
    internal static GoogleSheetsFileSpec Spec(GoogleSheetsExportJob job)=>new(job.FolderId,job.FolderBinding,job.Id,job.GenerationId,job.ManifestSha256,job.Mode);
    internal static ReadableGenerationArtifact Artifact(GoogleSheetsExportJob job)=>new(job.GenerationId,job.ManifestSha256);

    internal static IReadOnlyList<GoogleSheetsExportJob> List(DbWork db,string business)
    {
        if(!Identity(business))throw Invalid();
        return db.Read("SELECT Key,Value FROM AppMetadata WHERE Key GLOB $prefix ORDER BY Key;",r=>(Key:r.GetString(0),Json:r.GetString(1)),("$prefix",Prefix+business+"/*"))
            .Select(row=>Read(row.Json,row.Key,business)).ToArray();
    }

    internal static GoogleSheetsExportJob Get(DbWork db,string business,string id)
    {
        if(!Identity(business)||!NIdentity(id))throw Invalid();string key=Key(business,id);
        return Read(db.Scalar("SELECT Value FROM AppMetadata WHERE Key=$key;",("$key",key)) as string??throw Invalid(),key,business);
    }

    internal static void Insert(DbWork db,GoogleSheetsExportJob job)
    {
        Validate(job);db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$value);",("$key",Key(job.BusinessId,job.Id)),("$value",JsonSerializer.Serialize(job)));
    }

    private static GoogleSheetsExportJob Read(string json,string key,string business)
    {
        try
        {
            var job=JsonSerializer.Deserialize<GoogleSheetsExportJob>(json)??throw Invalid();Validate(job);
            if(job.BusinessId!=business||key!=Key(business,job.Id))throw Invalid();return job;
        }
        catch(JsonException){throw Invalid();}
    }

    private static void Validate(GoogleSheetsExportJob job)
    {
        if(job.Version!=1||!NIdentity(job.Id)||!Identity(job.BusinessId)||!Identity(job.StoreId)||!NIdentity(job.InstanceId)||!Identity(job.ConnectionId)||
            job.SourceWatermark<0||job.SchemaVersion<1||job.SchemaVersion>BusinessSchema.Version||job.Selection is null||!Utc(job.CapturedAtUtc)||!Utc(job.CreatedAtUtc))throw Invalid();
        try{job.Selection.Validate();Spec(job).Validate();}catch(ArgumentException){throw Invalid();}catch(GoogleConnectionFailure){throw Invalid();}
    }
    private static string Key(string business,string id)=>Prefix+business+"/"+id;
    private static bool Identity(string value)=>Guid.TryParse(value,out var id)&&id!=Guid.Empty;
    private static bool NIdentity(string value)=>Guid.TryParseExact(value,"N",out var id)&&id!=Guid.Empty;
    private static bool Utc(string value)=>DateTimeOffset.TryParseExact(value,"O",CultureInfo.InvariantCulture,DateTimeStyles.None,out var time)&&time.Offset==TimeSpan.Zero;
    private static InvalidDataException Invalid()=>new("The saved readable export job source needs review. Existing artifacts and history are preserved.");
}
