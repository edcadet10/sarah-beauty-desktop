using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Ownership;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class OwnershipExportContractTests
{
    [Fact]
    public void VersionThirtyThreeUpgradePreservesConfirmedInterestsAndItsEarlierSnapshot()
    {
        using var f=new BusinessFixture();var confirmed=new OwnerInterests(f.Store).Confirm(f.Owner,OwnerInterestConfirmationTests.Draft(f,0,new(2027,1,1),.6m));string json=JsonSerializer.Serialize(confirmed);
        f.Files.Execute("PRAGMA user_version=33;");var upgraded=new BusinessStore(f.Store.DatabasePath);Assert.Equal((long)BusinessSchema.Version,TestStore.Scalar(upgraded.DatabasePath,"PRAGMA user_version;"));Assert.Equal(f.Store.InstanceId,upgraded.InstanceId);
        Assert.Equal(json,JsonSerializer.Serialize(Assert.Single(new OwnerInterests(upgraded).History(f.Owner))));Assert.Equal(2L,TestStore.Scalar(upgraded.DatabasePath,"SELECT Watermark FROM Business;"));
        string snapshot=Assert.Single(f.Files.Snapshots());Assert.Equal(33L,TestStore.Scalar(snapshot,"PRAGMA user_version;"));Assert.Equal(json,TestStore.Scalar(snapshot,"SELECT SnapshotJson FROM OwnerInterestVersions;"));Assert.Equal(0L,TestStore.Scalar(snapshot,"SELECT count(*) FROM JournalLines;"));
    }
    [Fact]
    public async Task EarlierWriterSuccessRemainsHistoryWithoutCurrentOwnershipCoverageOrRetryAuthority()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=GoogleSheetsHealthTests.Complete(f,permit,artifact);
        var earlier=job with{SchemaVersion=33};store.Transaction(db=>db.Execute("UPDATE AppMetadata SET Value=$json WHERE Key=$key;",("$json",JsonSerializer.Serialize(earlier)),("$key","GoogleSheetsJob/"+job.BusinessId+"/"+job.Id)));
        var health=new GoogleSheetsHealthService(store).Current(job.Selection);Assert.Equal(earlier,health.LastSuccess!.Job);Assert.Null(health.MatchingSuccess);Assert.Null(health.ExportedWatermark);Assert.Equal("Never completed",health.Status);
        Assert.Equal(earlier,Assert.Single(new GoogleSheetsDeliveries(store).History(f.Ledger.Business.Owner)).Job);
        Assert.Throws<GoogleConnectionFailure>(()=>store.Read(db=>{new GoogleSheetsExportJobs(store).Require(db,permit,earlier);return true;}));Assert.True(Directory.Exists(new ReadableGenerationPaths(store.DatabasePath).Generation(job.GenerationId)));
    }
    [Fact]
    public void CompleteGenerationAndDictionaryIncludeEveryOwnershipTableAndItsJoinScope()
    {
        using var f=new BusinessFixture();var interest=new OwnerInterests(f.Store).Confirm(f.Owner,OwnerInterestConfirmationTests.Draft(f,0,new(2027,1,1),.6m));
        using var snapshot=new ReadableExportSnapshots(f.Store).Capture(f.Owner);var generation=ReadableExportGeneration.Build(snapshot,new(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31)));
        Assert.Equal(2,generation.Manifest.Version);Assert.Equal(BusinessSchema.Version,generation.Manifest.Source.SchemaVersion);Assert.Equal(30,generation.Manifest.Groups.Count);Assert.Equal(163,generation.Tables.Count);Assert.Equal(164,GoogleSheetsWorkbookSchema.Build(generation).Tabs.Count);
        var group=generation.Manifest.Groups.Single(g=>g.Name=="Ownership settings");Assert.Equal(4,group.Tables.Count);Assert.All(group.Tables,name=>Assert.Contains(generation.Tables,t=>t.Name==name));
        Assert.Equal(interest.Id,generation.Tables.Single(t=>t.Name=="Ownership coverage").Rows[0][3]);var dictionary=generation.Tables.Single(t=>t.Name=="Column dictionary");
        foreach(string name in group.Tables)Assert.Contains(dictionary.Rows,row=>row[0]==name&&row[2]=="OwnershipVersionId"&&row[6]=="Ownership confirmations"&&row[7]=="OwnershipVersionId");
        Assert.Equal("0",generation.Manifest.Journal.DebitCents);Assert.Equal("0",generation.Manifest.Journal.CreditCents);
    }
}
