using System.Text.Json;
using System.Text.Json.Nodes;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsSchemaInitializationTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(777)]
    public void FreshLayoutsBecomeExactWithoutDeletingTheLastSheet(int original)
    {
        using var f=new LedgerFixture();var schema=Schema(f);var current=Fresh(original);
        var changes=GoogleSheetsSchemaInitialization.Plan(schema,JsonSerializer.SerializeToElement(current),"fictional-workbook");
        Assert.True(changes.Operations>0);Assert.True(changes.Json.Length<GoogleSheetsValueRequests.MaximumRequestBytes);
        using var json=JsonDocument.Parse(changes.Json);Apply(current,json.RootElement.GetProperty("requests"));
        schema.Require(JsonSerializer.SerializeToElement(current),"fictional-workbook");
        var retry=GoogleSheetsSchemaInitialization.Plan(schema,JsonSerializer.SerializeToElement(current),"fictional-workbook");Assert.Equal(0,retry.Operations);
    }

    [Fact]
    public void UnrecognizedPartialLayoutCannotProduceInitializationRequests()
    {
        using var f=new LedgerFixture();var schema=Schema(f);var current=Fresh(777);current["sheets"]!.AsArray().Add(Fresh(778)["sheets"]![0]!.DeepClone());
        Assert.Throws<GoogleConnectionFailure>(()=>GoogleSheetsSchemaInitialization.Plan(schema,JsonSerializer.SerializeToElement(current),"fictional-workbook"));
    }

    internal static void Apply(JsonObject current,JsonElement requests)
    {
        var sheets=current["sheets"]!.AsArray();
        foreach(var request in requests.EnumerateArray())
        {
            if(request.TryGetProperty("addSheet",out var add))
            {
                var properties=JsonNode.Parse(add.GetProperty("properties").GetRawText())!.AsObject();int id=properties["sheetId"]!.GetValue<int>();
                Assert.DoesNotContain(sheets,s=>s!["properties"]!["sheetId"]!.GetValue<int>()==id);sheets.Add(new JsonObject{["properties"]=properties});
            }
            else if(request.TryGetProperty("deleteSheet",out var delete))
            {
                Assert.True(sheets.Count>1);int id=delete.GetProperty("sheetId").GetInt32();sheets.Remove(sheets.Single(s=>s!["properties"]!["sheetId"]!.GetValue<int>()==id));
            }
            else
            {
                var update=request.GetProperty("updateSheetProperties");var values=update.GetProperty("properties");int id=values.GetProperty("sheetId").GetInt32();
                var sheet=sheets.Single(s=>s!["properties"]!["sheetId"]!.GetValue<int>()==id)!;
                if(update.GetProperty("fields").GetString()=="index")
                {
                    int old=sheets.IndexOf(sheet),index=values.GetProperty("index").GetInt32();
                    if(index!=old&&index!=old+1){sheets.Remove(sheet);sheets.Insert(index>old?index-1:index,sheet);}
                }
                else foreach(var property in values.EnumerateObject())if(property.Name!="sheetId")sheet["properties"]![property.Name]=JsonNode.Parse(property.Value.GetRawText());
            }
            for(int i=0;i<sheets.Count;i++)sheets[i]!["properties"]!["index"]=i;
        }
    }

    internal static JsonObject Fresh(int id)=>new(){["spreadsheetId"]="fictional-workbook",["sheets"]=id<0?new JsonArray():new JsonArray(new JsonObject
        {["properties"]=new JsonObject{["sheetId"]=id,["title"]="Fictional default",["index"]=0,["sheetType"]="GRID",["gridProperties"]=new JsonObject{["rowCount"]=1000,["columnCount"]=26}}})};
    internal static GoogleSheetsWorkbookSchema Schema(LedgerFixture f)
    {
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        return GoogleSheetsWorkbookSchema.Build(ReadableExportGeneration.Build(source,new(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31))));
    }
}
