using System.Text.Json;

namespace SarahBeauty.Business.Google;

internal static class GoogleSheetsLiteralCells
{
    internal static void Require(JsonElement? cell,string expected)
    {
        if(cell is null){if(expected.Length!=0)throw Mismatch();return;}
        if(cell.Value.ValueKind!=JsonValueKind.Object)throw Mismatch();
        string[] properties=cell.Value.EnumerateObject().Select(p=>p.Name).ToArray();
        if(properties.Any(p=>p is not("userEnteredValue" or "effectiveValue"))||properties.Distinct(StringComparer.Ordinal).Count()!=properties.Length)throw Mismatch();
        if(Value(cell.Value,"userEnteredValue")!=expected||Value(cell.Value,"effectiveValue")!=expected)throw Mismatch();
    }

    private static string Value(JsonElement cell,string property)
    {
        if(!cell.TryGetProperty(property,out var value))return "";
        if(value.ValueKind!=JsonValueKind.Object)throw Mismatch();var fields=value.EnumerateObject().ToArray();
        if(fields.Length==0)return "";
        if(fields.Length!=1||fields[0].Name!="stringValue"||fields[0].Value.ValueKind!=JsonValueKind.String)throw Mismatch();
        return fields[0].Value.GetString()!;
    }

    private static GoogleConnectionFailure Mismatch()=>new("ReadableCellMismatch","Google returned cells that do not match the expected literal export. This generation cannot be marked complete.");
}
