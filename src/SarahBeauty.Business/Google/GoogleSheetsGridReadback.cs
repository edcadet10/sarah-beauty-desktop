using System.Text.Json;

namespace SarahBeauty.Business.Google;

internal static class GoogleSheetsGridReadback
{
    internal static void Require(JsonElement response,string spreadsheetId,int sheetId,SheetsWriteRectangle expected,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();
        if(sheetId<0||expected.StartRow is <0 or >999999||expected.StartColumn is <0 or >127||expected.Values.Count==0||expected.StartRow+(long)expected.Values.Count>1_000_000||expected.Values[0].Count is <1 or >128||expected.StartColumn+expected.Values[0].Count>128||expected.Values.Any(r=>r.Count!=expected.Values[0].Count))throw Mismatch();
        if(Text(response,"spreadsheetId")!=spreadsheetId)throw Mismatch();bool found=false;
        foreach(var sheet in Array(response,"sheets"))
        {
            var properties=Member(sheet,"properties");
            if(properties is null){if(Array(sheet,"data").Length!=0)throw Mismatch();continue;}
            int id=Number(properties.Value,"sheetId");string title=Text(properties.Value,"title");
            if(id!=sheetId){if(title==expected.Table||Array(sheet,"data").Length!=0)throw Mismatch();continue;}
            if(found||title!=expected.Table)throw Mismatch();found=true;
            var data=Array(sheet,"data");if(data.Length>1)throw Mismatch();
            if(data.Length==0)RequireRows([],expected,cancellation);
            else
            {
                if(Number(data[0],"startRow")!=expected.StartRow||Number(data[0],"startColumn")!=expected.StartColumn)throw Mismatch();
                RequireRows(Array(data[0],"rowData"),expected,cancellation);
            }
        }
        if(!found)throw Mismatch();
    }

    private static void RequireRows(JsonElement[] rows,SheetsWriteRectangle expected,CancellationToken cancellation)
    {
        if(rows.Length>expected.Values.Count)throw Mismatch();
        for(int r=0;r<expected.Values.Count;r++)
        {
            cancellation.ThrowIfCancellationRequested();var cells=r<rows.Length?Array(rows[r],"values"):[];
            if(cells.Length>expected.Values[r].Count)throw Mismatch();
            for(int c=0;c<expected.Values[r].Count;c++)GoogleSheetsLiteralCells.Require(c<cells.Length?cells[c]:null,expected.Values[r][c]);
        }
    }

    private static JsonElement? Member(JsonElement value,string name)
    {
        if(value.ValueKind!=JsonValueKind.Object)throw Mismatch();JsonElement? result=null;bool found=false;
        foreach(var property in value.EnumerateObject())if(property.Name==name){if(found)throw Mismatch();found=true;result=property.Value;}
        return result;
    }
    private static JsonElement[] Array(JsonElement value,string name)
    {
        var member=Member(value,name);if(member is null)return [];
        if(member.Value.ValueKind!=JsonValueKind.Array)throw Mismatch();return member.Value.EnumerateArray().ToArray();
    }
    private static string Text(JsonElement value,string name)
    {
        var member=Member(value,name);if(member is null||member.Value.ValueKind!=JsonValueKind.String)throw Mismatch();return member.Value.GetString()!;
    }
    private static int Number(JsonElement value,string name)
    {
        var member=Member(value,name);if(member is null)return 0;
        if(member.Value.ValueKind!=JsonValueKind.Number||!member.Value.TryGetInt32(out int result)||result<0)throw Mismatch();return result;
    }
    private static GoogleConnectionFailure Mismatch()=>new("ReadableGridMismatch","Google returned a different workbook, sheet, cell range, or grid shape. This generation cannot be marked complete.");
}
