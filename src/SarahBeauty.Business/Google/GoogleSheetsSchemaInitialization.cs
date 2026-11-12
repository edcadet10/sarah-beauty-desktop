using System.Text.Json;

namespace SarahBeauty.Business.Google;

internal sealed record GoogleSheetsSchemaChanges(int Operations,ReadOnlyMemory<byte> Json);
internal static class GoogleSheetsSchemaInitialization
{
    private const string LayoutFields="title,hidden,gridProperties.rowCount,gridProperties.columnCount,gridProperties.frozenRowCount";
    internal static GoogleSheetsSchemaChanges Plan(GoogleSheetsWorkbookSchema schema,JsonElement current,string spreadsheetId)
    {
        if(current.GetProperty("spreadsheetId").GetString()!=spreadsheetId)throw Unsupported();var sheets=current.GetProperty("sheets");
        if(sheets.ValueKind!=JsonValueKind.Array)throw Unsupported();
        if(sheets.GetArrayLength()==schema.Tabs.Count){schema.Require(current,spreadsheetId);return Encode([]);}
        if(sheets.GetArrayLength()>1)throw Unsupported();var requests=new List<object>();int? original=null;
        if(sheets.GetArrayLength()==1)
        {
            var properties=sheets[0].GetProperty("properties");original=properties.TryGetProperty("sheetId",out var id)?id.GetInt32():0;
            if(original<0||properties.TryGetProperty("sheetType",out var kind)&&kind.GetString()!="GRID")throw Unsupported();
            var reused=schema.Tabs.SingleOrDefault(t=>t.SheetId==original);
            object layout=reused is null?new{sheetId=original.Value,title="Staging "+schema.GenerationId,hidden=false,gridProperties=new{rowCount=1,columnCount=1,frozenRowCount=0}}:Properties(reused);
            requests.Add(new{updateSheetProperties=new{properties=layout,fields=LayoutFields}});
        }
        foreach(var tab in schema.Tabs.Where(t=>t.SheetId!=original))requests.Add(new{addSheet=new{properties=Properties(tab)}});
        if(original is {} removed&&!schema.Tabs.Any(t=>t.SheetId==removed))requests.Add(new{deleteSheet=new{sheetId=removed}});
        foreach(var tab in schema.Tabs.OrderBy(t=>t.Index))requests.Add(new{updateSheetProperties=new{properties=new{sheetId=tab.SheetId,index=tab.Index},fields="index"}});
        return Encode(requests);
    }

    private static object Properties(GoogleSheetsTab tab)=>new{sheetId=tab.SheetId,title=tab.Name,hidden=false,gridProperties=new{rowCount=tab.Rows,columnCount=tab.Columns,frozenRowCount=tab.FrozenRows}};
    private static GoogleSheetsSchemaChanges Encode(List<object> requests)
    {
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(new{requests=requests.ToArray(),includeSpreadsheetInResponse=false});
        if(bytes.Length>GoogleSheetsValueRequests.MaximumRequestBytes)throw Unsupported();return new(requests.Count,bytes);
    }
    private static GoogleConnectionFailure Unsupported()=>new("ReadableInitialSchemaUnsupported","This workbook does not have a supported staging layout. Review the app-owned file and job identity before initialization.");
}
