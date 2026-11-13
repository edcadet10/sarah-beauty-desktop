using System.Text.Json;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class DocumentDecisionExportContractTests
{
    internal static ReadableExportSelection Selection=>new(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31));
    [Fact]
    public void CompleteGenerationIncludesDecisionTablesWithVersionOwnerAndDateDictionaryFacts()
    {
        using var f=new BusinessFixture();var version=QuoteTerminalDecisionTests.Issue(f);var decision=new DocumentDecisions(f.Store).RecordQuote(f.Owner,QuoteTerminalDecisionTests.Draft(version,DocumentDecisionAction.DeclineQuote),BusinessFixture.Phrase,Guid.NewGuid().ToString());using var source=new ReadableExportSnapshots(f.Store).Capture(f.Owner);var generation=ReadableExportGeneration.Build(source,Selection);
        Assert.Equal(3,generation.Manifest.Version);Assert.Equal(31,generation.Manifest.Groups.Count);Assert.Equal(165,generation.Tables.Count);Assert.Equal(166,GoogleSheetsWorkbookSchema.Build(generation).Tabs.Count);var group=generation.Manifest.Groups.Single(g=>g.Name=="Document decisions");Assert.Equal(new[]{"Document decisions","Document decision coverage"},group.Tables);Assert.Equal(decision.Id,Assert.Single(generation.Tables.Single(t=>t.Name=="Document decisions").Rows)[0]);
        var dictionary=generation.Tables.Single(t=>t.Name=="Column dictionary");Assert.Contains(dictionary.Rows,r=>r[0]=="Document decisions"&&r[2]=="DocumentDecisionId"&&r[6]=="Document decisions"&&r[7]=="DocumentDecisionId");Assert.Contains(dictionary.Rows,r=>r[0]=="Document decisions"&&r[2]=="VersionId"&&r[6]=="Document versions");Assert.Contains(dictionary.Rows,r=>r[0]=="Document decisions"&&r[2]=="RecordedByOwnerId"&&r[6]=="Owners");Assert.Contains(dictionary.Rows,r=>r[0]=="Document decisions"&&r[2]=="DecisionDate"&&r[4].Contains("yyyy-MM-dd",StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(()=>ReadableExportGeneration.Verify(generation.Manifest,generation.ManifestSha256,generation.Tables.Where(t=>t.Name!="Document decisions")));
    }
    [Theory]
    [InlineData("format")][InlineData("writer")]
    public void RehashedOlderFormatOrWriterCannotClaimCompleteDecisionCoverage(string defect)
    {
        using var f=new BusinessFixture();using var source=new ReadableExportSnapshots(f.Store).Capture(f.Owner);var generation=ReadableExportGeneration.Build(source,Selection);var earlier=defect=="format"?generation.Manifest with{Version=2}:generation.Manifest with{Source=generation.Manifest.Source with{SchemaVersion=37}};
        string digest=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(earlier)));Assert.Throws<InvalidDataException>(()=>ReadableExportGeneration.Verify(earlier,digest,generation.Tables));
    }
    [Fact]
    public async Task EarlierWriterSuccessRemainsHistoryWithoutCurrentCoverageOrRetryAuthority()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=GoogleSheetsHealthTests.Complete(f,permit,artifact);var earlier=job with{SchemaVersion=37};store.Transaction(db=>db.Execute("UPDATE AppMetadata SET Value=$json WHERE Key=$key;",("$json",JsonSerializer.Serialize(earlier)),("$key","GoogleSheetsJob/"+job.BusinessId+"/"+job.Id)));
        var health=new GoogleSheetsHealthService(store).Current(job.Selection);Assert.Equal(earlier,health.LastSuccess!.Job);Assert.Null(health.MatchingSuccess);Assert.Null(health.ExportedWatermark);Assert.Equal(earlier,Assert.Single(new GoogleSheetsDeliveries(store).History(f.Ledger.Business.Owner)).Job);Assert.Throws<GoogleConnectionFailure>(()=>store.Read(db=>{new GoogleSheetsExportJobs(store).Require(db,permit,earlier);return true;}));Assert.True(Directory.Exists(new ReadableGenerationPaths(store.DatabasePath).Generation(job.GenerationId)));
    }
    [Fact]
    public void WriterUpgradePreservesRetainedDecisionsAndAnOpenableEarlierSnapshot()
    {
        using var f=new BusinessFixture();var version=QuoteTerminalDecisionTests.Issue(f);var decision=new DocumentDecisions(f.Store).RecordQuote(f.Owner,QuoteTerminalDecisionTests.Draft(version,DocumentDecisionAction.WithdrawQuote),BusinessFixture.Phrase,Guid.NewGuid().ToString());long watermark=Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));BusinessProfileSchemaTests.RemoveEmpty(f);f.Files.Execute("PRAGMA user_version=37;");
        var upgraded=new BusinessStore(f.Store.DatabasePath);Assert.Equal((long)BusinessSchema.Version,TestStore.Scalar(upgraded.DatabasePath,"PRAGMA user_version;"));Assert.Equal(decision,Assert.Single(new DocumentDecisions(upgraded).History(f.Owner)));Assert.Equal(watermark,TestStore.Scalar(upgraded.DatabasePath,"SELECT Watermark FROM Business;"));string path=Assert.Single(f.Files.Snapshots());Assert.Equal(37L,TestStore.Scalar(path,"PRAGMA user_version;"));Assert.Equal(1L,TestStore.Scalar(path,"SELECT count(*) FROM DocumentDecisions;"));new BusinessStore(upgraded.DatabasePath);Assert.Single(f.Files.Snapshots());
    }
}
