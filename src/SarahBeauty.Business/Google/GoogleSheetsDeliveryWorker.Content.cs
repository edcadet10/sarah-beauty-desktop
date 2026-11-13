using SarahBeauty.Business.Exports;

namespace SarahBeauty.Business.Google;

public sealed partial class GoogleSheetsDeliveryWorker
{
    private async Task<GoogleSheetsVerifiedDelivery> DeliverContentAsync(GoogleOutputPermit permit,GoogleSheetsExportJob job,GoogleSheetsAttempt attempt,ReadableExportGeneration generation,GoogleSheetsDeliveryAccess access,CancellationToken cancellation)
    {
        var schema=GoogleSheetsWorkbookSchema.Build(generation,cancellation);var spec=GoogleSheetsJobRecords.Spec(job);string account=permit.Connection.Account.Account.PermissionId;
        var file=await new GoogleSheetsWorkbookCreation(_store,_client).ResolveAsync(permit,job,access.TokenAsync,cancellation).ConfigureAwait(false);
        if(file.State=="staging")await WriteStagingAsync(permit,job,attempt,generation,schema,file.FileId,account,access,cancellation).ConfigureAwait(false);
        else await VerifyPublishedAsync(job,generation,schema,file.FileId,account,access,cancellation).ConfigureAwait(false);
        await new GoogleSheetsWorkbookReadback(_client).LayoutAsync(schema,file.FileId,access.TokenAsync,cancellation).ConfigureAwait(false);await access.PrivateAsync("verified",cancellation).ConfigureAwait(false);
        return new(file.FileId,job.GenerationId,job.ManifestSha256,job.SourceWatermark,DateTimeOffset.UtcNow.ToString("O"));
    }
    private async Task WriteStagingAsync(GoogleOutputPermit permit,GoogleSheetsExportJob job,GoogleSheetsAttempt attempt,ReadableExportGeneration generation,GoogleSheetsWorkbookSchema schema,string fileId,string account,GoogleSheetsDeliveryAccess access,CancellationToken cancellation)
    {
        var spec=GoogleSheetsJobRecords.Spec(job);await access.FolderAsync(cancellation).ConfigureAwait(false);
        await new GoogleSheetsSchemaTransport(_client).EnsureAsync(schema,spec,fileId,account,access.TokenAsync,cancellation).ConfigureAwait(false);
        await WriteBatchesAsync(fileId,GoogleSheetsValueRequests.Build(generation,cancellation),access,cancellation).ConfigureAwait(false);FailurePoint?.Invoke("Source values sent");
        var readback=new GoogleSheetsWorkbookReadback(_client);await readback.SourcesAsync(generation,spec,fileId,account,access.TokenAsync,cancellation).ConfigureAwait(false);
        var publication=new GoogleSheetsPublicationCheckpoints(_store).Prepare(permit,job,attempt,cancellation);var status=GoogleSheetsDeliveryStatus.Build(job,publication,generation,cancellation);
        await WriteBatchesAsync(fileId,GoogleSheetsValueRequests.ForTables(job.GenerationId,job.SourceWatermark,[status],cancellation:cancellation),access,cancellation).ConfigureAwait(false);
        await readback.StatusAsync(schema,status,spec,fileId,account,access.TokenAsync,cancellation).ConfigureAwait(false);await access.PrivateAsync("staging",cancellation).ConfigureAwait(false);
        await new GoogleSheetsPublicationTransport(_client).PublishAsync(spec,fileId,account,access.TokenAsync,cancellation).ConfigureAwait(false);
    }
    private async Task VerifyPublishedAsync(GoogleSheetsExportJob job,ReadableExportGeneration generation,GoogleSheetsWorkbookSchema schema,string fileId,string account,GoogleSheetsDeliveryAccess access,CancellationToken cancellation)
    {
        access.Require();var publication=_store.Read(db=>GoogleSheetsPublicationRecords.Get(db,job))??throw new GoogleConnectionFailure("CompletionUnconfirmed","This verified remote file has no matching publication checkpoint. Preserve it for review.");
        if(publication.FileId!=fileId)throw GoogleSheetsPublicationRecords.Invalid();var status=GoogleSheetsDeliveryStatus.Build(job,publication,generation,cancellation);var readback=new GoogleSheetsWorkbookReadback(_client);
        await readback.SourcesAsync(generation,GoogleSheetsJobRecords.Spec(job),fileId,account,access.TokenAsync,cancellation,"verified").ConfigureAwait(false);
        await readback.StatusAsync(schema,status,GoogleSheetsJobRecords.Spec(job),fileId,account,access.TokenAsync,cancellation,"verified").ConfigureAwait(false);
    }
    private async Task WriteBatchesAsync(string fileId,IEnumerable<GoogleSheetsValueBatch> batches,GoogleSheetsDeliveryAccess access,CancellationToken cancellation)
    {
        foreach(var batch in batches)
        {
            await access.PrivateAsync("staging",cancellation).ConfigureAwait(false);await new GoogleSheetsValuesTransport(_client).WriteAsync(fileId,batch,access.TokenAsync,cancellation).ConfigureAwait(false);
        }
    }
}
