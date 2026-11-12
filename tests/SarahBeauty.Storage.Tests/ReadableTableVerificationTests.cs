using SarahBeauty.Business.Exports;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableTableVerificationTests
{
    [Theory]
    [InlineData("generation")]
    [InlineData("watermark")]
    [InlineData("name")]
    [InlineData("header")]
    [InlineData("value")]
    [InlineData("order")]
    [InlineData("width")]
    [InlineData("missing row")]
    public void AlteredSourceShapeOrContentsCannotPassTheirOriginalDigest(string defect)
    {
        using var f=new LedgerFixture();using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var builder=new ReadableTableBuilder(source.Source,new(),"Facts","Id","Cents");
        builder.Add("Fictional A",long.MaxValue);builder.Add("Fictional B",0);var original=builder.Freeze();
        var changed=defect switch
        {
            "generation"=>original with{GenerationId=Guid.NewGuid().ToString("N")},
            "watermark"=>original with{Watermark=original.Watermark+1},
            "name"=>original with{Name="Different facts"},
            "header"=>original with{Columns=new[]{"DifferentId","Cents"}},
            "value"=>original with{Rows=new[]{new[]{"Fictional A","9223372036854775806"},original.Rows[1]}},
            "order"=>original with{Rows=original.Rows.Reverse().ToArray()},
            "width"=>original with{Rows=new[]{new[]{"Fictional A"}}},
            _=>original with{Rows=new[]{original.Rows[0]}}
        };
        Assert.Throws<InvalidDataException>(()=>ReadableTableVerification.CopyAll(source.Source,new[]{changed},new()));
    }

    [Fact]
    public void VerifiedCopiesFreezeMutableInputsAndKeepLiteralExactValues()
    {
        using var f=new LedgerFixture();using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var builder=new ReadableTableBuilder(source.Source,new(),"Facts","Literal","Cents","Unknown");
        builder.Add("=Fictional literal",long.MaxValue,null);var original=builder.Freeze();
        var columns=original.Columns.ToArray();var row=original.Rows.Single().ToArray();var rows=new[]{row};
        var input=original with{Columns=columns,Rows=rows};
        var result=ReadableTableVerification.CopyAll(source.Source,new[]{input},new());
        columns[0]="Changed";row[0]="Changed";
        Assert.Equal(original.Sha256,result[0].Sha256);Assert.Equal(original.Columns,result[0].Columns);Assert.Equal(original.Rows[0],result[0].Rows[0]);
        Assert.Throws<NotSupportedException>(()=>((IList<ReadableExportTable>)result).Clear());
        Assert.Throws<NotSupportedException>(()=>((IList<string>)result[0].Rows[0])[0]="Changed");
    }

    [Fact]
    public void EmptyDuplicateAndOversizedSetsAreRejected()
    {
        using var f=new LedgerFixture();using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableContactTables.Build(source,new());
        Assert.Throws<InvalidDataException>(()=>ReadableTableVerification.CopyAll(source.Source,Array.Empty<ReadableExportTable>(),new()));
        Assert.Throws<InvalidDataException>(()=>ReadableTableVerification.CopyAll(source.Source,new[]{tables[0],tables[0]},new()));
        Assert.Throws<InvalidDataException>(()=>ReadableTableVerification.CopyAll(source.Source,tables,new(maximumCells:1)));
        Assert.Throws<InvalidDataException>(()=>ReadableTableVerification.CopyAll(source.Source,tables,new(maximumBytes:1)));
    }
}
