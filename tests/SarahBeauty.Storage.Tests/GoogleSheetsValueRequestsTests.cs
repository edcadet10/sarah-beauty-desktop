using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsValueRequestsTests
{
    private const string Generation="00000000000000000000000000000001";

    [Fact]
    public void MaximumSupportedWideRowFitsProductionRequestsThroughColumnSplits()
    {
        var builder=new ReadableTableBuilder(Generation,42,new(),"Maximum row",Enumerable.Range(1,128).Select(i=>"Field"+i).ToArray());
        builder.Add(Enumerable.Repeat<object?>(new string('<',32768),128).ToArray());var table=builder.Freeze();
        var batches=GoogleSheetsValueRequests.ForTables(Generation,42,new[]{table}).ToArray();
        Assert.True(batches.Length>1);Assert.All(batches,b=>Assert.True(b.Json.Length<=GoogleSheetsValueRequests.MaximumRequestBytes));
        Assert.Equal(256,batches.SelectMany(b=>b.Rectangles).Sum(r=>r.Values.Sum(row=>row.Count)));
    }

    [Fact]
    public void CompleteGenerationProducesBoundedRequestsForAllManifestCells()
    {
        using var f=new LedgerFixture();using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var generation=ReadableExportGeneration.Build(source,new(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31)));
        var batches=GoogleSheetsValueRequests.Build(generation).ToArray();
        Assert.Equal(163,batches.SelectMany(b=>b.Rectangles).Select(r=>r.Table).Distinct().Count());
        Assert.Equal(generation.Manifest.Cells,batches.SelectMany(b=>b.Rectangles).Sum(r=>r.Values.Sum(row=>(long)row.Count)));
        Assert.All(batches,b=>Assert.True(b.Json.Length<=GoogleSheetsValueRequests.MaximumRequestBytes));
    }

    [Fact]
    public void RowAndColumnSplitsCoverEveryLiteralCellExactlyOnceWithinActualJsonBounds()
    {
        var builder=new ReadableTableBuilder(Generation,42,new(),"Fictional wide facts",Enumerable.Range(1,8).Select(i=>"Field"+i).ToArray());
        builder.Add(Enumerable.Range(1,8).Select(i=>(object?)("=Fictional literal "+i+new string('<',60))).ToArray());
        for(int i=0;i<30;i++)builder.Add(i,long.MaxValue,"",1.2500m,new DateOnly(2027,1,10),"+Literal","@Literal","é\n\t");
        var table=builder.Freeze();var batches=GoogleSheetsValueRequests.ForTables(Generation,42,new[]{table},1024).ToArray();
        Assert.True(batches.Length>1);Assert.Contains(batches.SelectMany(b=>b.Rectangles),r=>r.StartColumn>0);
        var expected=new[]{table.Columns}.Concat(table.Rows).ToArray();var visited=new HashSet<(int Row,int Column)>();
        foreach(var batch in batches)
        {
            Assert.True(batch.Json.Length<=1024);Assert.Equal(Generation,batch.GenerationId);Assert.Equal(42,batch.Watermark);
            using var json=JsonDocument.Parse(batch.Json);Assert.Equal("RAW",json.RootElement.GetProperty("valueInputOption").GetString());
            var data=json.RootElement.GetProperty("data").EnumerateArray().ToArray();Assert.Equal(batch.Rectangles.Count,data.Length);
            for(int k=0;k<data.Length;k++)
            {
                var rectangle=batch.Rectangles[k];Assert.Equal(rectangle.Range,data[k].GetProperty("range").GetString());Assert.Equal("ROWS",data[k].GetProperty("majorDimension").GetString());
                var rows=data[k].GetProperty("values").EnumerateArray().ToArray();
                for(int r=0;r<rows.Length;r++)
                {
                    var cells=rows[r].EnumerateArray().ToArray();
                    for(int c=0;c<cells.Length;c++){Assert.Equal(JsonValueKind.String,cells[c].ValueKind);Assert.True(visited.Add((rectangle.StartRow+r,rectangle.StartColumn+c)));Assert.Equal(expected[rectangle.StartRow+r][rectangle.StartColumn+c],cells[c].GetString());}
                }
            }
        }
        Assert.Equal(expected.Sum(r=>r.Count),visited.Count);
    }

    [Fact]
    public void A1ColumnsRangeLimitsAndCancellationRemainExplicit()
    {
        var builder=new ReadableTableBuilder(Generation,42,new(),"Facts","Fact");builder.Add("Fictional");var table=builder.Freeze();
        Assert.Equal("'Facts'!Z10:AA10",new SheetsWriteRectangle("Facts",9,25,new[]{new[]{"A","B"}}).Range);
        Assert.Equal("'Facts'!DX1:DX1",new SheetsWriteRectangle("Facts",0,127,new[]{new[]{"A"}}).Range);
        var many=Enumerable.Range(0,101).Select(i=>table with{Name="Facts "+i}).Select(t=>
        {
            var copy=new ReadableTableBuilder(Generation,42,new(),t.Name,"Fact");copy.Add("Fictional");return copy.Freeze();
        });
        var batches=GoogleSheetsValueRequests.ForTables(Generation,42,many).ToArray();Assert.Equal(2,batches.Length);Assert.Equal(100,batches[0].Rectangles.Count);
        using var cancel=new CancellationTokenSource();cancel.Cancel();Assert.Throws<OperationCanceledException>(()=>GoogleSheetsValueRequests.ForTables(Generation,42,new[]{table},cancellation:cancel.Token).ToArray());
    }
}
