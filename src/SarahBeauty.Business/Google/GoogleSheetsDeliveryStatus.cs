using SarahBeauty.Business.Exports;

namespace SarahBeauty.Business.Google;

internal static class GoogleSheetsDeliveryStatus
{
    internal static ReadableExportTable Build(GoogleSheetsExportJob job,GoogleSheetsPublication publication,ReadableExportGeneration generation,CancellationToken cancellation=default)
    {
        GoogleSheetsPublicationRecords.Require(publication,job);var verified=ReadableExportGeneration.Verify(generation.Manifest,job.ManifestSha256,generation.Tables,cancellation);var manifest=verified.Manifest;var source=manifest.Source;
        if(source.GenerationId!=job.GenerationId||source.BusinessId!=job.BusinessId||source.StoreId!=job.StoreId||source.InstanceId!=job.InstanceId||source.Mode!=job.Mode||source.SchemaVersion!=job.SchemaVersion||source.Watermark!=job.SourceWatermark||source.CapturedAtUtc!=job.CapturedAtUtc||manifest.Selection!=job.Selection)throw GoogleSheetsPublicationRecords.Invalid();
        var rows=new List<(string Field,object? Value)>
        {
            ("Format","Sarah Beauty readable delivery v1"),("Delivery state","Verified"),("Business mode",job.Mode),("Business ID",job.BusinessId),("Store ID",job.StoreId),("Writable instance ID",job.InstanceId),
            ("Export job ID",job.Id),("Generation ID",job.GenerationId),("Manifest SHA-256",job.ManifestSha256),("Source watermark",job.SourceWatermark),("Source captured UTC",job.CapturedAtUtc),("Source readback checkpoint UTC",publication.SourceReadbackAtUtc),
            ("Financial from",job.Selection.FinancialFrom),("Financial through",job.Selection.FinancialThrough),("Planning as of",job.Selection.PlanningAsOf),("Events from",job.Selection.EventFrom),("Events through",job.Selection.EventThrough),
            ("Source tables",manifest.Tables.Count),("Source data rows",manifest.DataRows),("Source cells including headers",manifest.Cells),("Source raw UTF-8 bytes",manifest.RawUtf8Bytes),
            ("Journal lines",manifest.Journal.Rows),("Journal debit cents",manifest.Journal.DebitCents),("Journal credit cents",manifest.Journal.CreditCents),("Remote file ID",publication.FileId),
            ("Record authority","Desktop business store"),("Sheet edits","Not imported into the desktop books"),("Privacy","Last checked during delivery; sharing can change later"),
            ("Completion evidence","All source and status cells require independent typed readback")
        };
        if(rows.Count>=GoogleSheetsWorkbookSchema.StatusRows)throw GoogleSheetsPublicationRecords.Invalid();
        var table=new ReadableTableBuilder(job.GenerationId,job.SourceWatermark,new(64,cancellation:cancellation),GoogleSheetsWorkbookSchema.DeliveryStatus,"Field","Value");
        foreach(var row in rows)table.Add(row.Field,row.Value);for(int i=rows.Count;i<GoogleSheetsWorkbookSchema.StatusRows-1;i++)table.Add("","");return table.Freeze();
    }
}
