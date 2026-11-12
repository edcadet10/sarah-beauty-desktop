using System.Text.Json;
using SarahBeauty.Business.Exports;

namespace SarahBeauty.Business.Google;

internal sealed record GoogleSheetsTab(int SheetId,int Index,string Name,int Rows,int Columns,int FrozenRows,bool SourceTable);
internal sealed record GoogleSheetsWorkbookSchema(string GenerationId,string ManifestSha256,IReadOnlyList<GoogleSheetsTab> Tabs,long Cells)
{
    internal const string DeliveryStatus="Export delivery status";
    internal const int StatusRows=32;
    internal const string Fields="spreadsheetId,sheets(properties(sheetId,title,index,sheetType,hidden,gridProperties(rowCount,columnCount,frozenRowCount)))";

    internal static GoogleSheetsWorkbookSchema Build(ReadableExportGeneration generation,CancellationToken cancellation=default)
    {
        var verified=ReadableExportGeneration.Verify(generation.Manifest,generation.ManifestSha256,generation.Tables,cancellation);
        var ids=verified.Tables.Select((t,i)=>(t.Name,Id:i)).ToDictionary(t=>t.Name,t=>t.Id,StringComparer.Ordinal);
        if(ids.ContainsKey(DeliveryStatus))throw new InvalidDataException("The operational status sheet name cannot identify an immutable source table.");
        var first=new[]{"Start Here",DeliveryStatus,"Export conventions","Business table inventory","Column dictionary"};
        var names=first.Concat(verified.Tables.Select(t=>t.Name).Except(first,StringComparer.Ordinal)).ToArray();var tables=verified.Tables.ToDictionary(t=>t.Name,StringComparer.Ordinal);
        var tabs=new List<GoogleSheetsTab>();
        for(int i=0;i<names.Length;i++)
        {
            cancellation.ThrowIfCancellationRequested();string name=names[i];
            if(name==DeliveryStatus){tabs.Add(new(verified.Tables.Count,i,name,StatusRows,2,1,false));continue;}
            var table=tables[name];int rows=checked(table.Rows.Count+1);tabs.Add(new(ids[name],i,name,rows,table.Columns.Count,rows>1?1:0,true));
        }
        return new(verified.Manifest.Source.GenerationId,verified.ManifestSha256,tabs.AsReadOnly(),tabs.Sum(t=>(long)t.Rows*t.Columns));
    }

    internal void Require(JsonElement response,string spreadsheetId)
    {
        if(response.GetProperty("spreadsheetId").GetString()!=spreadsheetId)throw Mismatch();var sheets=response.GetProperty("sheets");
        if(sheets.ValueKind!=JsonValueKind.Array||sheets.GetArrayLength()!=Tabs.Count)throw Mismatch();var seen=new HashSet<int>();
        foreach(var sheet in sheets.EnumerateArray())
        {
            var properties=sheet.GetProperty("properties");int id=Number(properties,"sheetId");var expected=Tabs.SingleOrDefault(t=>t.SheetId==id);
            if(expected is null||!seen.Add(id)||properties.GetProperty("title").GetString()!=expected.Name||Number(properties,"index")!=expected.Index||
                properties.TryGetProperty("sheetType",out var kind)&&kind.GetString()!="GRID"||properties.TryGetProperty("hidden",out var hidden)&&hidden.ValueKind!=JsonValueKind.False)throw Mismatch();
            var grid=properties.GetProperty("gridProperties");
            if(Number(grid,"rowCount")!=expected.Rows||Number(grid,"columnCount")!=expected.Columns||Number(grid,"frozenRowCount")!=expected.FrozenRows)throw Mismatch();
        }
    }

    private static int Number(JsonElement value,string name)
    {
        if(!value.TryGetProperty(name,out var number))return 0;
        if(number.ValueKind!=JsonValueKind.Number||!number.TryGetInt32(out int result)||result<0)throw Mismatch();return result;
    }
    private static GoogleConnectionFailure Mismatch()=>new("ReadableSchemaMismatch","Google returned a different sheet inventory, identity, or grid layout. This generation cannot be marked complete.");
}
