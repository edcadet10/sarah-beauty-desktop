using SarahBeauty.Business.Exports;

namespace SarahBeauty.Business.Google;

internal static class GoogleSheetsRequestShapes
{
    internal static SheetsWriteRectangle Copy(SheetsWriteRectangle rectangle,ReadableExportBudget budget)
    {
        if(string.IsNullOrWhiteSpace(rectangle.Table)||rectangle.Table.Length>64||rectangle.Table.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not(' ' or '_' or '-'))||
            rectangle.StartRow is <0 or >999999||rectangle.StartColumn is <0 or >127||rectangle.Values.Count==0||rectangle.StartRow+(long)rectangle.Values.Count>1_000_000)throw Invalid();
        int columns=rectangle.Values[0].Count;
        if(columns is <1 or >128||rectangle.StartColumn+columns>128)throw Invalid();var rows=new List<IReadOnlyList<string>>();
        foreach(var row in rectangle.Values)
        {
            if(row.Count!=columns||row.Any(value=>value is null))throw Invalid();budget.Consume(row);rows.Add(Array.AsReadOnly(row.ToArray()));
        }
        return new(rectangle.Table,rectangle.StartRow,rectangle.StartColumn,rows.AsReadOnly());
    }
    private static InvalidDataException Invalid()=>new("The literal Sheets rectangle has unsupported source shape or bounds.");
}
