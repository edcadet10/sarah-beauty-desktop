using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed class GoogleSheetsExportJobs(BusinessStore store)
{
    private readonly GoogleOutputSettings _settings=new(store);

    public IReadOnlyList<GoogleSheetsExportJob> History(OwnerSession owner)=>store.ReadSnapshot(db=>
    {
        store.RequireSession(db,owner);return GoogleSheetsJobRecords.List(db,owner.BusinessId)
            .OrderByDescending(job=>job.CreatedAtUtc,StringComparer.Ordinal).ThenBy(job=>job.Id,StringComparer.Ordinal).ToArray();
    });

    internal GoogleSheetsExportJob Reserve(GoogleOutputPermit permit,ReadableGenerationArtifact artifact,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();store.Read(db=>{RequirePermission(db,permit);return true;});
        var generation=new ReadableGenerationFiles(store).Load(artifact,cancellation);
        return store.Transaction(db=>
        {
            cancellation.ThrowIfCancellationRequested();RequirePermission(db,permit);var source=generation.Manifest.Source;var policy=permit.Policy;
            var job=new GoogleSheetsExportJob(1,Guid.NewGuid().ToString("N"),source.BusinessId,source.StoreId,source.InstanceId,policy.ConnectionId,policy.FolderId,policy.FolderBinding,
                source.GenerationId,generation.ManifestSha256,source.Mode,source.SchemaVersion,source.Watermark,source.CapturedAtUtc,generation.Manifest.Selection,DateTimeOffset.UtcNow.ToString("O"));
            RequireSource(db,permit,job);
            var prior=GoogleSheetsJobRecords.List(db,policy.BusinessId).SingleOrDefault(value=>value.GenerationId==job.GenerationId&&SameDestination(value,job));
            if(prior is not null)
            {
                if(prior with{Id=job.Id,CreatedAtUtc=job.CreatedAtUtc}!=job)throw Changed();Require(db,permit,prior);cancellation.ThrowIfCancellationRequested();return prior;
            }
            GoogleSheetsJobRecords.Insert(db,job);cancellation.ThrowIfCancellationRequested();return job;
        });
    }

    internal void Require(DbWork db,GoogleOutputPermit permit,GoogleSheetsExportJob job)
    {
        RequirePermission(db,permit);RequireSource(db,permit,job);
        if(GoogleSheetsJobRecords.Get(db,job.BusinessId,job.Id)!=job)throw Changed();
    }

    internal static void RequireGeneration(GoogleSheetsExportJob job,ReadableExportGeneration generation)
    {
        var source=generation.Manifest.Source;
        if(generation.ManifestSha256!=job.ManifestSha256||source.GenerationId!=job.GenerationId||source.BusinessId!=job.BusinessId||source.StoreId!=job.StoreId||source.InstanceId!=job.InstanceId||source.Mode!=job.Mode||source.SchemaVersion!=job.SchemaVersion||source.Watermark!=job.SourceWatermark||source.CapturedAtUtc!=job.CapturedAtUtc||generation.Manifest.Selection!=job.Selection)throw Changed();
    }

    private void RequireSource(DbWork db,GoogleOutputPermit permit,GoogleSheetsExportJob job)
    {
        var policy=permit.Policy;
        if(job.BusinessId!=policy.BusinessId||job.StoreId!=store.StoreId||job.InstanceId!=store.InstanceId||job.ConnectionId!=policy.ConnectionId||
            job.FolderId!=policy.FolderId||job.FolderBinding!=policy.FolderBinding||job.SchemaVersion!=BusinessSchema.Version||
            job.Mode!=(string?)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")||
            db.Scalar("SELECT Id FROM Business WHERE Id=$business AND Watermark>=$watermark;",("$business",job.BusinessId),("$watermark",job.SourceWatermark)) is null)throw Changed();
    }

    private void RequirePermission(DbWork db,GoogleOutputPermit permit)
    {
        if(permit.Kind!=GoogleOutputKind.SheetsExport)throw new UnauthorizedAccessException("Readable Sheets export permission is required.");_settings.Require(db,permit);
    }
    private static bool SameDestination(GoogleSheetsExportJob first,GoogleSheetsExportJob second)=>first.InstanceId==second.InstanceId&&first.ConnectionId==second.ConnectionId&&first.FolderId==second.FolderId&&first.FolderBinding==second.FolderBinding;
    private static GoogleConnectionFailure Changed()=>new("GoogleSheetsSourceChanged","The saved readable source or Google destination changed. Review this export before retrying.");
}
