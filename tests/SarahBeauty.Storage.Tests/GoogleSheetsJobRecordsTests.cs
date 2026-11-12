using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsJobRecordsTests
{
    [Fact]
    public void ImmutableSourcePersistsAcrossReopenWithoutRenewingOwnerOrChangingWatermark()
    {
        using var f=new LedgerFixture();var job=Job(f);f.Business.Store.Transaction(db=>{GoogleSheetsJobRecords.Insert(db,job);return true;});f.Business.Owner.Lock();
        var reopened=new BusinessStore(f.Business.Store.DatabasePath);var read=reopened.Read(db=>GoogleSheetsJobRecords.Get(db,job.BusinessId,job.Id));Assert.Equal(job,read);
        Assert.Equal(job,Assert.Single(reopened.Read(db=>GoogleSheetsJobRecords.List(db,job.BusinessId))));
        Assert.Equal(job.SourceWatermark,reopened.Read(db=>Convert.ToInt64(db.Scalar("SELECT Watermark FROM Business WHERE Id=$id;",("$id",job.BusinessId)))));
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);Assert.Equal(job.GenerationId,GoogleSheetsJobRecords.Artifact(read).GenerationId);
        Assert.Equal(job.ManifestSha256,GoogleSheetsJobRecords.Spec(read).ManifestSha256);
        using var foreign=new BusinessFixture();Assert.Empty(reopened.Read(db=>GoogleSheetsJobRecords.List(db,foreign.Owner.BusinessId)));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("id")]
    [InlineData("generation")]
    [InlineData("digest")]
    [InlineData("range")]
    [InlineData("timestamp")]
    [InlineData("schema")]
    [InlineData("mode")]
    [InlineData("watermark")]
    public void CorruptStoredSourceCannotBeUsedAsARetryAuthority(string defect)
    {
        using var f=new LedgerFixture();var original=Job(f);var job=defect switch
        {
            "version"=>original with{Version=2},"id"=>original with{Id=Guid.NewGuid().ToString("N")},"generation"=>original with{GenerationId=new string('0',32)},
            "digest"=>original with{ManifestSha256="bad"},"range"=>original with{Selection=original.Selection with{EventFrom=original.Selection.PlanningAsOf.AddDays(-1)}},
            "timestamp"=>original with{CapturedAtUtc="not a UTC timestamp"},"schema"=>original with{SchemaVersion=BusinessSchema.Version+1},
            "mode"=>original with{Mode="unknown"},_=>original with{SourceWatermark=-1}
        };
        string key="GoogleSheetsJob/"+original.BusinessId+"/"+original.Id;
        f.Business.Store.Transaction(db=>db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$value);",("$key",key),("$value",JsonSerializer.Serialize(job))));
        Assert.Throws<InvalidDataException>(()=>f.Business.Store.Read(db=>GoogleSheetsJobRecords.Get(db,original.BusinessId,original.Id)));
    }

    internal static GoogleSheetsExportJob Job(LedgerFixture f)
    {
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var selection=new ReadableExportSelection(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31));
        var generation=ReadableExportGeneration.Build(source,selection);
        return new(1,Guid.NewGuid().ToString("N"),source.Source.BusinessId,source.Source.StoreId,source.Source.InstanceId,Guid.NewGuid().ToString(),"fictional-folder",Guid.NewGuid().ToString("N"),
            source.Source.GenerationId,generation.ManifestSha256,source.Source.Mode,source.Source.SchemaVersion,source.Source.Watermark,source.Source.CapturedAtUtc,selection,DateTimeOffset.UtcNow.ToString("O"));
    }
}
