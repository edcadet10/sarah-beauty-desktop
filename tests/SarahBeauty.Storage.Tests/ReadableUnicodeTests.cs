using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableUnicodeTests
{
    private const string Generation="00000000000000000000000000000001";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UnpairedSurrogatesCannotEnterAReadableTable(bool high)
    {
        string value="Fictional "+(high?'\uD800':'\uDC00');
        var builder=new ReadableTableBuilder(Generation,42,new(),"Unicode facts","Fact");
        Assert.Throws<InvalidDataException>(()=>builder.Add(value));
    }

    [Fact]
    public void EmojiAndDistinctComposedFormsKeepTheirExactOriginalText()
    {
        string value="Fictional 💄 é e\u0301 �";
        var builder=new ReadableTableBuilder(Generation,42,new(),"Unicode facts","Fact");builder.Add(value);var table=builder.Freeze();
        using var stream=new MemoryStream();ReadableTableStreams.Write(stream,table);stream.Position=0;
        var source=new ReadableGenerationSource(1,Generation,"00000000-0000-0000-0000-000000000002","Fictional Unicode",
            "00000000-0000-0000-0000-000000000003","00000000000000000000000000000004","TEST",BusinessSchema.Version,42,"2027-01-10T00:00:00.0000000+00:00");
        var reloaded=ReadableTableStreams.Read(stream,source,ReadableGenerationFacts.Table(table),new());
        Assert.Equal(value,Assert.Single(Assert.Single(reloaded.Rows)));Assert.Equal(table.Sha256,reloaded.Sha256);
    }
}
