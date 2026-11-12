using System.Text.Json;
using System.Text.Json.Nodes;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsWorkbookSchemaTests
{
    [Fact]
    public void SourceTabsAndSeparateStatusHaveExactStableIdsDimensionsAndDisplayOrder()
    {
        using var f=new LedgerFixture();var generation=Generation(f);var schema=GoogleSheetsWorkbookSchema.Build(generation);
        Assert.Equal(160,schema.Tabs.Count);Assert.Equal(159,schema.Tabs.Count(t=>t.SourceTable));
        Assert.Equal("Start Here",schema.Tabs[0].Name);Assert.Equal(GoogleSheetsWorkbookSchema.DeliveryStatus,schema.Tabs[1].Name);
        Assert.Equal(Enumerable.Range(0,160),schema.Tabs.Select(t=>t.Index));Assert.Equal(160,schema.Tabs.Select(t=>t.SheetId).Distinct().Count());
        Assert.Equal(generation.Manifest.Cells+64,schema.Cells);
        foreach(var table in generation.Tables)
        {
            var tab=schema.Tabs.Single(t=>t.Name==table.Name);Assert.Equal(table.Rows.Count+1,tab.Rows);Assert.Equal(table.Columns.Count,tab.Columns);
            Assert.Equal(Array.IndexOf(generation.Tables.ToArray(),table),tab.SheetId);Assert.Equal(tab.Rows>1?1:0,tab.FrozenRows);
        }
        schema.Require(JsonSerializer.SerializeToElement(Response(schema)),"fictional-workbook");
        var again=GoogleSheetsWorkbookSchema.Build(generation);Assert.Equal(JsonSerializer.Serialize(schema),JsonSerializer.Serialize(again));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("id")]
    [InlineData("title")]
    [InlineData("index")]
    [InlineData("type")]
    [InlineData("rows")]
    [InlineData("columns")]
    [InlineData("frozen")]
    [InlineData("hidden")]
    public void AlteredSchemaCannotPassItsCapturedPlan(string defect)
    {
        using var f=new LedgerFixture();var schema=GoogleSheetsWorkbookSchema.Build(Generation(f));var response=Response(schema);var sheets=response["sheets"]!.AsArray();
        var properties=sheets[0]!["properties"]!;var grid=properties["gridProperties"]!;
        switch(defect)
        {
            case "missing":sheets.RemoveAt(0);break;case "extra":sheets.Add(sheets[0]!.DeepClone());break;
            case "id":properties["sheetId"]=999;break;case "title":properties["title"]="Changed";break;case "index":properties["index"]=999;break;
            case "type":properties["sheetType"]="DATA_SOURCE";break;case "rows":grid["rowCount"]=999;break;case "columns":grid["columnCount"]=999;break;
            case "frozen":grid["frozenRowCount"]=999;break;case "hidden":properties["hidden"]=true;break;
        }
        Assert.Throws<GoogleConnectionFailure>(()=>schema.Require(JsonSerializer.SerializeToElement(response),"fictional-workbook"));
    }

    private static ReadableExportGeneration Generation(LedgerFixture f)
    {
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        return ReadableExportGeneration.Build(source,new(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31)));
    }
    private static JsonObject Response(GoogleSheetsWorkbookSchema schema)=>new()
    {
        ["spreadsheetId"]="fictional-workbook",["sheets"]=new JsonArray(schema.Tabs.Select(t=>(JsonNode?)new JsonObject
        {
            ["properties"]=new JsonObject{["sheetId"]=t.SheetId,["title"]=t.Name,["index"]=t.Index,["sheetType"]="GRID",["hidden"]=false,
                ["gridProperties"]=new JsonObject{["rowCount"]=t.Rows,["columnCount"]=t.Columns,["frozenRowCount"]=t.FrozenRows}}
        }).ToArray())
    };
}
