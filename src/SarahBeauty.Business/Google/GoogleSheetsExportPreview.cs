using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed record GoogleSheetsPreviewTable(string Name,int DataRows,int Columns,long Cells,long RawUtf8Bytes);
public sealed record GoogleSheetsExportPreview(string GenerationId,string BusinessName,string Mode,int SchemaVersion,long SourceWatermark,string CapturedAtUtc,ReadableExportSelection Selection,
    long DataRows,long SourceCells,long PlannedWorkbookCells,long RawUtf8Bytes,long JournalLines,string JournalDebitCents,string JournalCreditCents,IReadOnlyList<GoogleSheetsPreviewTable> Tables,IReadOnlyList<string> Notices);

public sealed class GoogleSheetsPreview(BusinessStore store)
{
    internal Action? BeforeReturn {get;set;}
    public GoogleSheetsExportPreview Build(OwnerSession owner,ReadableExportSelection selection,CancellationToken cancellation=default)
    {
        ArgumentNullException.ThrowIfNull(selection);selection.Validate();cancellation.ThrowIfCancellationRequested();
        using var snapshot=new ReadableExportSnapshots(store).Capture(owner,cancellation);var generation=ReadableExportGeneration.Build(snapshot,selection,cancellation);
        var preview=Describe(generation);BeforeReturn?.Invoke();cancellation.ThrowIfCancellationRequested();store.Read(db=>{store.RequireSession(db,owner);return true;});cancellation.ThrowIfCancellationRequested();return preview;
    }
    private static GoogleSheetsExportPreview Describe(ReadableExportGeneration generation)
    {
        var manifest=generation.Manifest;var source=manifest.Source;
        var tables=manifest.Tables.Select(table=>new GoogleSheetsPreviewTable(table.Name,table.DataRows,table.Columns,table.Cells,table.RawUtf8Bytes)).ToArray();
        var notices=new[]{Value(generation,"Start Here","OwnerInterestStatus"),Value(generation,"Start Here","DocumentDecisionStatus"),Value(generation,"Export conventions","Record scope"),Value(generation,"Export conventions","Private omissions")};
        return new(source.GenerationId,source.BusinessName,source.Mode,source.SchemaVersion,source.Watermark,source.CapturedAtUtc,manifest.Selection,manifest.DataRows,manifest.Cells,
            checked(manifest.Cells+GoogleSheetsWorkbookSchema.StatusRows*2L),manifest.RawUtf8Bytes,manifest.Journal.Rows,manifest.Journal.DebitCents,manifest.Journal.CreditCents,Array.AsReadOnly(tables),Array.AsReadOnly(notices));
    }
    private static string Value(ReadableExportGeneration generation,string table,string property)=>generation.Tables.Single(value=>value.Name==table).Rows.Single(row=>row[0]==property)[1];
}
