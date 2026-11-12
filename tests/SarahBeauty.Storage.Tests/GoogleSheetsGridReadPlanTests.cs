using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsGridReadPlanTests
{
    private const string Generation="00000000000000000000000000000001";

    [Fact]
    public void MaximumEscapedRowHasCompleteBoundedTypedReadbackRectangles()
    {
        var builder=new ReadableTableBuilder(Generation,42,new(),"Read facts",Enumerable.Range(1,128).Select(i=>"Field"+i).ToArray());
        builder.Add(Enumerable.Repeat<object?>(new string('<',32768),128).ToArray());var table=builder.Freeze();
        var requests=GoogleSheetsGridReadPlan.ForTable("Fictional-workbook",7,table).ToArray();var visited=new HashSet<(int Row,int Column)>();var expected=new[]{table.Columns}.Concat(table.Rows).ToArray();
        Assert.True(requests.Length>1);Assert.Contains(requests,r=>r.Expected.StartColumn>0);
        foreach(var request in requests)
        {
            Assert.Equal("https",request.Uri.Scheme);Assert.Equal("sheets.googleapis.com",request.Uri.Host);Assert.Equal(7,request.SheetId);
            Assert.True(request.EstimatedResponseBytes<=GoogleSheetsReadSlices.MaximumEstimatedBytes);
            byte[] bytes=Response(request);Assert.True(bytes.Length<=request.EstimatedResponseBytes);Assert.True(bytes.Length<GoogleApiJson.MaximumResponseBytes);
            using var json=JsonDocument.Parse(bytes);GoogleSheetsGridReadback.Require(json.RootElement,request.SpreadsheetId,request.SheetId,request.Expected);
            for(int r=0;r<request.Expected.Values.Count;r++)for(int c=0;c<request.Expected.Values[r].Count;c++)
            {
                Assert.True(visited.Add((request.Expected.StartRow+r,request.Expected.StartColumn+c)));
                Assert.Equal(expected[request.Expected.StartRow+r][request.Expected.StartColumn+c],request.Expected.Values[r][c]);
            }
        }
        Assert.Equal(256,visited.Count);
    }

    [Fact]
    public void RowSplitsFieldMaskAndCancellationRemainBoundToLiteralTableData()
    {
        var builder=new ReadableTableBuilder(Generation,42,new(),"Read facts","Literal");
        for(int i=0;i<300;i++)builder.Add("=Fictional literal "+new string('é',1000));var table=builder.Freeze();
        var requests=GoogleSheetsGridReadPlan.ForTable("Fictional-workbook",0,table).ToArray();Assert.True(requests.Length>1);
        Assert.Equal(301,requests.Sum(r=>r.Expected.Values.Count));Assert.Equal(0,requests[0].Expected.StartRow);
        foreach(var request in requests)
        {
            var query=request.Uri.Query.TrimStart('?').Split('&').Select(p=>p.Split('=',2)).ToDictionary(p=>p[0],p=>Uri.UnescapeDataString(p[1]));
            Assert.Equal("false",query["prettyPrint"]);Assert.Equal(GoogleSheetsGridReadPlan.Fields,query["fields"]);Assert.Equal(request.Expected.Range,query["ranges"]);
            Assert.DoesNotContain("=Fictional",request.Uri.AbsoluteUri);Assert.DoesNotContain("access_token",request.Uri.AbsoluteUri);
        }
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        Assert.Throws<OperationCanceledException>(()=>GoogleSheetsGridReadPlan.ForTable("Fictional-workbook",0,table,cancel.Token).ToArray());
        Assert.Throws<InvalidDataException>(()=>GoogleSheetsGridReadPlan.ForTable("Fictional-workbook",0,table with{Sha256=new string('0',64)}).ToArray());
    }

    private static byte[] Response(GoogleSheetsGridReadRequest request)=>JsonSerializer.SerializeToUtf8Bytes(new
    {
        spreadsheetId=request.SpreadsheetId,
        sheets=new[]{new
        {
            properties=new{sheetId=request.SheetId,title=request.Expected.Table},
            data=new[]{new{startRow=request.Expected.StartRow,startColumn=request.Expected.StartColumn,
                rowData=request.Expected.Values.Select(row=>new{values=row.Select(value=>new{userEnteredValue=new{stringValue=value},effectiveValue=new{stringValue=value}}).ToArray()}).ToArray()}}
        }}
    });
}
