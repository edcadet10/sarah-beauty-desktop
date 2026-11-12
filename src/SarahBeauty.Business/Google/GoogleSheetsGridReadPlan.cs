using SarahBeauty.Business.Exports;

namespace SarahBeauty.Business.Google;

internal sealed record GoogleSheetsGridReadRequest(string SpreadsheetId,int SheetId,SheetsWriteRectangle Expected,int EstimatedResponseBytes,Uri Uri);
internal static class GoogleSheetsGridReadPlan
{
    internal const string Fields="spreadsheetId,sheets(properties(sheetId,title),data(startRow,startColumn,rowData(values(userEnteredValue,effectiveValue))))";
    internal static IEnumerable<GoogleSheetsGridReadRequest> ForTable(string spreadsheetId,int sheetId,ReadableExportTable table,CancellationToken cancellation=default)
    {
        GoogleDriveFolders.RequireId(spreadsheetId);if(sheetId<0)throw new ArgumentException("Choose a supported sheet identity.");
        var verified=ReadableTableVerification.CopyAll(table.GenerationId,table.Watermark,new[]{table},new(cancellation:cancellation)).Single();
        foreach(var rectangle in GoogleSheetsReadSlices.Build(verified,cancellation))
        {
            cancellation.ThrowIfCancellationRequested();int estimate=GoogleSheetsReadSlices.Estimate(rectangle);
            if(estimate>GoogleSheetsReadSlices.MaximumEstimatedBytes)throw new InvalidDataException("The typed readback rectangle exceeds its response estimate budget.");
            yield return new(spreadsheetId,sheetId,rectangle,estimate,Address(spreadsheetId,rectangle));
        }
    }
    internal static Uri Address(string spreadsheetId,SheetsWriteRectangle rectangle)
    {
        GoogleDriveFolders.RequireId(spreadsheetId);
        return new("https://sheets.googleapis.com/v4/spreadsheets/"+Uri.EscapeDataString(spreadsheetId)+"?prettyPrint=false&fields="+Uri.EscapeDataString(Fields)+"&ranges="+Uri.EscapeDataString(rectangle.Range));
    }
}
