using System.Text;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableTableStreamsTests
{
    private static readonly ReadableGenerationSource Identity=new(1,"00000000000000000000000000000001","00000000-0000-0000-0000-000000000002","Fictional stream fixture",
        "00000000-0000-0000-0000-000000000003","00000000000000000000000000000004","TEST",BusinessSchema.Version,42,"2027-01-10T00:00:00.0000000+00:00");

    [Fact]
    public void EscapedUnicodeAndExactLiteralValuesRoundTripAcrossBufferBoundaries()
    {
        var builder=new ReadableTableBuilder(Identity.GenerationId,Identity.Watermark,new(),"Stream facts","Text","Cents","Unknown");
        builder.Add("=Fictional literal\n\t"+new string('é',12000),long.MaxValue,null);builder.Add("Second",0,null);var table=builder.Freeze();
        using var stream=new MemoryStream();ReadableTableStreams.Write(stream,table);Assert.Equal(3,stream.ToArray().Count(b=>b==(byte)'\n'));
        stream.Position=0;var actual=ReadableTableStreams.Read(stream,Identity,ReadableGenerationFacts.Table(table),new());
        Assert.Equal(table.Sha256,actual.Sha256);Assert.Equal(table.Columns,actual.Columns);
        Assert.Equal(table.Rows.SelectMany(r=>r),actual.Rows.SelectMany(r=>r));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("nested")]
    [InlineData("number")]
    [InlineData("null")]
    [InlineData("width")]
    [InlineData("truncated")]
    [InlineData("changed")]
    [InlineData("digest")]
    public void IncompleteChangedOrNonliteralRowsCannotPassTheirRecordedFacts(string defect)
    {
        var table=Facts();using var written=new MemoryStream();ReadableTableStreams.Write(written,table);
        string text=Encoding.UTF8.GetString(written.ToArray());string[] lines=text.Split('\n');var expected=ReadableGenerationFacts.Table(table);
        string changed=defect switch
        {
            "missing"=>string.Join('\n',lines.Take(2))+"\n","extra"=>text+"[\"Extra\",\"0\"]\n",
            "nested"=>lines[0]+"\n[[\"Nested\"],\"0\"]\n"+lines[2]+"\n",
            "number"=>lines[0]+"\n[\"Number\",1]\n"+lines[2]+"\n",
            "null"=>lines[0]+"\n[null,\"0\"]\n"+lines[2]+"\n",
            "width"=>lines[0]+"\n[\"Wide\",\"0\",\"Extra\"]\n"+lines[2]+"\n",
            "truncated"=>text[..^10],"changed"=>text.Replace("Second","Altered"),_=>text
        };
        if(defect=="digest")expected=expected with{Sha256=new string('0',64)};
        using var source=new MemoryStream(Encoding.UTF8.GetBytes(changed));
        Assert.Throws<InvalidDataException>(()=>ReadableTableStreams.Read(source,Identity,expected,new()));
    }

    [Fact]
    public void BoundsCancellationAndNewEmptyDestinationAreEnforced()
    {
        var table=Facts();using var stream=new MemoryStream();ReadableTableStreams.Write(stream,table);stream.Position=0;
        Assert.Throws<ArgumentException>(()=>ReadableTableStreams.Write(stream,table));
        Assert.Throws<InvalidDataException>(()=>ReadableTableStreams.Read(stream,Identity,ReadableGenerationFacts.Table(table),new(maximumCells:3)));
        using var longLine=new MemoryStream(Encoding.UTF8.GetBytes("12345678\n"));
        Assert.Throws<InvalidDataException>(()=>new ReadableJsonLines(longLine).Read(7,default));
        using var cancel=new CancellationTokenSource();cancel.Cancel();using var empty=new MemoryStream();
        Assert.Throws<OperationCanceledException>(()=>ReadableTableStreams.Write(empty,table,cancel.Token));
        stream.Position=0;Assert.Throws<OperationCanceledException>(()=>ReadableTableStreams.Read(stream,Identity,ReadableGenerationFacts.Table(table),new(),cancel.Token));
        using var invalidUtf8=new MemoryStream(new byte[]{(byte)'[',(byte)'"',255,(byte)'"',(byte)']',(byte)'\n'});
        Assert.Throws<InvalidDataException>(()=>ReadableTableStreams.Read(invalidUtf8,Identity,ReadableGenerationFacts.Table(table),new()));
    }

    private static ReadableExportTable Facts()
    {
        var builder=new ReadableTableBuilder(Identity.GenerationId,Identity.Watermark,new(),"Stream facts","Text","Cents");
        builder.Add("=Fictional literal",long.MaxValue);builder.Add("Second",0);return builder.Freeze();
    }
}
