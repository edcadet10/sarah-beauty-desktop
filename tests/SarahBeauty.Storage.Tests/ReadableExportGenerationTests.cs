using System.Text.Json;
using System.Security.Cryptography;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableExportGenerationTests
{
    private static readonly ReadableExportSelection Dates=new(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31));

    [Fact]
    public void MinimalTableIdentityKeepsTheEstablishedJsonDigestFormat()
    {
        var builder=new ReadableTableBuilder("00000000000000000000000000000001",42,new(),"Hash compatibility","Literal","Cents");
        builder.Add("Fictional",long.MaxValue);
        Assert.Equal("323C7E3DD2D8FEB6EB5F612EF5D43844F58837B8BCC4C4D47B2F9857C6415F8C",builder.Freeze().Sha256);
    }

    [Fact]
    public void CompleteProjectionSurvivesSerializationWithOneTrustedManifestAndExactTotals()
    {
        using var f=new LedgerFixture();f.Post(f.Draft(FinancialKind.Receipt,5000));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);f.Business.Owner.Lock();
        var generation=ReadableExportGeneration.Build(source,Dates);
        var manifest=JsonSerializer.Deserialize<ReadableGenerationManifest>(JsonSerializer.SerializeToUtf8Bytes(generation.Manifest))!;
        var tables=JsonSerializer.Deserialize<ReadableExportTable[]>(JsonSerializer.SerializeToUtf8Bytes(generation.Tables))!;
        var verified=ReadableExportGeneration.Verify(manifest,generation.ManifestSha256,tables);
        Assert.Equal(JsonSerializer.Serialize(generation),JsonSerializer.Serialize(verified));
        Assert.Equal(159,verified.Tables.Count);Assert.Equal(29,verified.Manifest.Groups.Count);
        Assert.Equal("5000",verified.Manifest.Journal.DebitCents);Assert.Equal("5000",verified.Manifest.Journal.CreditCents);Assert.Equal(2,verified.Manifest.Journal.Rows);
        Assert.Equal(verified.Tables.Sum(t=>(t.Rows.Count+1L)*t.Columns.Count),verified.Manifest.Cells);
        Assert.DoesNotContain(BusinessFixture.Phrase,JsonSerializer.Serialize(verified));Assert.DoesNotContain("\"Facts\":",JsonSerializer.Serialize(verified.Manifest.Source));
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
        Assert.Throws<InvalidDataException>(()=>ReadableExportGeneration.Verify(manifest,new string('0',64),tables));
        Assert.Throws<InvalidDataException>(()=>ReadableExportGeneration.Verify(manifest,generation.ManifestSha256,tables.Skip(1)));
        Assert.Throws<InvalidDataException>(()=>ReadableExportGeneration.Verify(manifest with{Source=manifest.Source with{Watermark=manifest.Source.Watermark+1}},generation.ManifestSha256,tables));
        var changed=tables.ToArray();changed[0]=changed[0] with{GenerationId=Guid.NewGuid().ToString("N")};
        Assert.Throws<InvalidDataException>(()=>ReadableExportGeneration.Verify(manifest,generation.ManifestSha256,changed));
        void RejectEvenWithMatchingDigest(ReadableGenerationManifest invalid)
        {
            string hash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(invalid)));
            Assert.Throws<InvalidDataException>(()=>ReadableExportGeneration.Verify(invalid,hash,tables));
        }
        RejectEvenWithMatchingDigest(manifest with{Groups=manifest.Groups.Skip(1).ToArray()});
        RejectEvenWithMatchingDigest(manifest with{DataRows=manifest.DataRows+1});
        RejectEvenWithMatchingDigest(manifest with{Version=2});
    }

    [Fact]
    public void JournalCrossCheckSupportsTotalsBeyondInt64AndRejectsUnbalancedOrFractionalValues()
    {
        var builder=new ReadableTableBuilder("00000000000000000000000000000001",42,new(),"Journal lines","DebitCents","CreditCents");
        builder.Add(long.MaxValue,long.MaxValue);builder.Add(long.MaxValue,long.MaxValue);
        var table=builder.Freeze();var facts=ReadableGenerationFacts.Journal(table);
        Assert.Equal("18446744073709551614",facts.DebitCents);Assert.Equal(facts.DebitCents,facts.CreditCents);
        Assert.Throws<InvalidDataException>(()=>ReadableGenerationFacts.Journal(table with{Rows=new[]{new[]{"1","0"}}}));
        Assert.Throws<InvalidDataException>(()=>ReadableGenerationFacts.Journal(table with{Rows=new[]{new[]{"1.25","1.25"}}}));
        Assert.Throws<InvalidDataException>(()=>ReadableGenerationFacts.Journal(table with{Rows=new[]{new[]{"-1","-1"}}}));
    }
}
