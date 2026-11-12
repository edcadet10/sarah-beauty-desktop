using System.Text.Json;
using System.Text.Json.Nodes;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsGridReadbackTests
{
    private static readonly SheetsWriteRectangle Expected=new("Facts",2,1,new[]{new[]{"=Fictional literal","9223372036854775807",""},new[]{"","",""}});

    [Fact]
    public void ExactTypedGridAllowsOnlyExpectedOmittedTrailingBlankRowsAndColumns()
    {
        var response=Response();response["sheets"]!.AsArray().Add(new JsonObject{["properties"]=new JsonObject{["sheetId"]=8,["title"]="Other"}});
        GoogleSheetsGridReadback.Require(JsonSerializer.SerializeToElement(response),"Fictional workbook",7,Expected);
        var empty=JsonSerializer.SerializeToElement(new{spreadsheetId="Fictional workbook",sheets=new[]{new{properties=new{title="Facts"},data=new[]{new{}}}}});
        GoogleSheetsGridReadback.Require(empty,"Fictional workbook",0,new("Facts",0,0,new[]{new[]{"",""}}));
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        Assert.Throws<OperationCanceledException>(()=>GoogleSheetsGridReadback.Require(empty,"Fictional workbook",0,Expected,cancel.Token));
    }

    [Theory]
    [InlineData("workbook")]
    [InlineData("sheet")]
    [InlineData("title")]
    [InlineData("row")]
    [InlineData("column")]
    [InlineData("extra row")]
    [InlineData("extra column")]
    [InlineData("formula")]
    [InlineData("number")]
    [InlineData("missing value")]
    [InlineData("foreign grid")]
    [InlineData("duplicate grid")]
    [InlineData("null row")]
    public void WrongIdentityOffsetsCoverageOrCellKindsPreventVerification(string defect)
    {
        var response=Response();var sheet=response["sheets"]![0]!;var grid=sheet["data"]![0]!;
        var rows=grid["rowData"]!.AsArray();var values=rows[0]!["values"]!.AsArray();
        switch(defect)
        {
            case "workbook":response["spreadsheetId"]="Other workbook";break;
            case "sheet":sheet["properties"]!["sheetId"]=9;break;
            case "title":sheet["properties"]!["title"]="Other title";break;
            case "row":grid["startRow"]=3;break;
            case "column":grid["startColumn"]=2;break;
            case "extra row":rows.Add(new JsonObject());rows.Add(new JsonObject());break;
            case "extra column":values.Add(new JsonObject());values.Add(new JsonObject());break;
            case "formula":values[0]!["userEnteredValue"]=new JsonObject{["formulaValue"]="=\"=Fictional literal\""};break;
            case "number":values[1]!["userEnteredValue"]=new JsonObject{["numberValue"]=9223372036854775807d};break;
            case "missing value":values.Clear();break;
            case "foreign grid":response["sheets"]!.AsArray().Add(new JsonObject{["properties"]=new JsonObject{["sheetId"]=8,["title"]="Other"},["data"]=new JsonArray(new JsonObject())});break;
            case "duplicate grid":sheet["data"]!.AsArray().Add(grid.DeepClone());break;
            case "null row":rows[0]=null;break;
        }
        var error=Assert.Throws<GoogleConnectionFailure>(()=>GoogleSheetsGridReadback.Require(JsonSerializer.SerializeToElement(response),"Fictional workbook",7,Expected));
        Assert.DoesNotContain("=Fictional literal",error.Message);
    }

    private static JsonObject Response()=>new()
    {
        ["spreadsheetId"]="Fictional workbook",
        ["sheets"]=new JsonArray(new JsonObject
        {
            ["properties"]=new JsonObject{["sheetId"]=7,["title"]="Facts"},
            ["data"]=new JsonArray(new JsonObject{["startRow"]=2,["startColumn"]=1,["rowData"]=new JsonArray(new JsonObject
                {["values"]=new JsonArray(Cell("=Fictional literal"),Cell("9223372036854775807"))})})
        })
    };
    private static JsonObject Cell(string value)=>new(){["userEnteredValue"]=new JsonObject{["stringValue"]=value},["effectiveValue"]=new JsonObject{["stringValue"]=value}};
}
