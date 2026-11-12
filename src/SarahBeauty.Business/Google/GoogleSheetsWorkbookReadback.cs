using SarahBeauty.Business.Exports;

namespace SarahBeauty.Business.Google;

internal sealed class GoogleSheetsWorkbookReadback(HttpClient? client=null)
{
    internal async Task SourcesAsync(ReadableExportGeneration generation,GoogleSheetsFileSpec spec,string fileId,string account,Func<CancellationToken,Task<string>> access,CancellationToken cancellation=default,string state="staging")
    {
        var verified=ReadableExportGeneration.Verify(generation.Manifest,spec.ManifestSha256,generation.Tables,cancellation);var schema=GoogleSheetsWorkbookSchema.Build(verified,cancellation);RequireSource(schema,spec);
        var files=new GoogleSheetsFiles(client);await files.VerifyAsync(spec,fileId,account,access,cancellation,state).ConfigureAwait(false);await LayoutAsync(schema,fileId,access,cancellation).ConfigureAwait(false);
        foreach(var table in verified.Tables)await TableAsync(schema,table,fileId,access,cancellation).ConfigureAwait(false);
        await LayoutAsync(schema,fileId,access,cancellation).ConfigureAwait(false);await files.VerifyAsync(spec,fileId,account,access,cancellation,state).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();
    }

    internal async Task StatusAsync(GoogleSheetsWorkbookSchema schema,ReadableExportTable status,GoogleSheetsFileSpec spec,string fileId,string account,Func<CancellationToken,Task<string>> access,CancellationToken cancellation=default,string state="staging")
    {
        RequireSource(schema,spec);
        if(status.GenerationId!=schema.GenerationId||status.Name!=GoogleSheetsWorkbookSchema.DeliveryStatus||status.Rows.Count!=GoogleSheetsWorkbookSchema.StatusRows-1||!status.Columns.SequenceEqual(new[]{"Field","Value"}))throw GoogleSheetsPublicationRecords.Invalid();
        var files=new GoogleSheetsFiles(client);await files.VerifyAsync(spec,fileId,account,access,cancellation,state).ConfigureAwait(false);await LayoutAsync(schema,fileId,access,cancellation).ConfigureAwait(false);
        await TableAsync(schema,status,fileId,access,cancellation).ConfigureAwait(false);await files.VerifyAsync(spec,fileId,account,access,cancellation,state).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();
    }

    internal async Task LayoutAsync(GoogleSheetsWorkbookSchema schema,string fileId,Func<CancellationToken,Task<string>> access,CancellationToken cancellation=default)
    {
        GoogleDriveFolders.RequireId(fileId);cancellation.ThrowIfCancellationRequested();string token=await access(cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();
        using var request=GoogleDriveFolders.Request(HttpMethod.Get,"https://sheets.googleapis.com/v4/spreadsheets/"+fileId+"?prettyPrint=false&fields="+Uri.EscapeDataString(GoogleSheetsWorkbookSchema.Fields),token);
        await GoogleApiJson.ReadAsync(client??GoogleApiJson.Client,request,root=>{schema.Require(root,fileId);return true;},cancellation).ConfigureAwait(false);
    }
    private async Task TableAsync(GoogleSheetsWorkbookSchema schema,ReadableExportTable table,string fileId,Func<CancellationToken,Task<string>> access,CancellationToken cancellation)
    {
        var tab=schema.Tabs.Single(t=>t.Name==table.Name);var transport=new GoogleSheetsValuesTransport(client);
        foreach(var request in GoogleSheetsGridReadPlan.ForTable(fileId,tab.SheetId,table,cancellation))await transport.ReadAsync(request,access,cancellation).ConfigureAwait(false);
    }
    private static void RequireSource(GoogleSheetsWorkbookSchema schema,GoogleSheetsFileSpec spec)
    {
        spec.Validate();if(schema.GenerationId!=spec.GenerationId||schema.ManifestSha256!=spec.ManifestSha256)throw new GoogleConnectionFailure("ReadableSchemaSourceMismatch","The workbook readback and file do not identify the same readable source.");
    }
}
