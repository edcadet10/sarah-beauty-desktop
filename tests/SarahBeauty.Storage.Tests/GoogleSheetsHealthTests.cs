using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsHealthTests
{
    [Fact]
    public async Task CoverageKeepsSelectedDatesAndHighestWatermarkDistinctFromTheLatestOutcome()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var health=new GoogleSheetsHealthService(f.Ledger.Business.Store);Assert.Equal("Not configured",health.Current().Status);
        var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var first=Complete(f,permit,artifact);Assert.Equal("Succeeded",health.Current(first.Selection).Status);Assert.Equal("Report dates required",health.Current().Status);
        f.Ledger.Post(f.Ledger.Draft(FinancialKind.Receipt,12345));Assert.Equal("Pending",health.Current(first.Selection).Status);
        var selection=first.Selection with{FinancialThrough=new(2027,2,1)};using var snapshot=new ReadableExportSnapshots(f.Ledger.Business.Store).Capture(permit);var generation=ReadableExportGeneration.Build(snapshot,selection);
        var second=Complete(f,permit,new ReadableGenerationFiles(f.Ledger.Business.Store).Stage(generation));var current=health.Current(first.Selection);
        Assert.Equal("Pending",current.Status);Assert.Equal(first.SourceWatermark,current.ExportedWatermark);Assert.Equal(second,current.LastSuccess!.Job);Assert.Equal("Succeeded",health.Current(selection).Status);
        long watermark=f.Watermark;f.Ledger.Business.Owner.Lock();Assert.Equal(current,health.Current(first.Selection));Assert.Equal(watermark,f.Watermark);Assert.Equal(DateTimeOffset.MinValue,f.Ledger.Business.Owner.ExpiresAt);
        Assert.Throws<UnauthorizedAccessException>(()=>health.Current(f.Ledger.Business.Owner,selection));
    }
    [Fact]
    public async Task PersistedRunningIsReviewRequiredAndFailuresDoNotBecomeSuccessfulCoverage()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);var health=new GoogleSheetsHealthService(store);
        Assert.Equal("Never completed",health.Current(job.Selection).Status);var deliveries=new GoogleSheetsDeliveries(store);var attempt=deliveries.Begin(permit,job);Assert.Equal("Needs review",health.Current(job.Selection).Status);
        deliveries.Fail(attempt,new IOException("Fictional private details"));var failed=health.Current(job.Selection);Assert.Equal("Failed",failed.Status);Assert.Null(failed.ExportedWatermark);Assert.Equal("ReadableSourceUnavailable",failed.FailureCode);
        Assert.Equal(job,Complete(f,permit,artifact));Assert.Equal("Succeeded",health.Current(job.Selection).Status);
        await f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,false,permit.Policy.Revision));var disabled=health.Current(job.Selection);Assert.Equal("Disabled",disabled.Status);Assert.Equal(job,disabled.LastSuccess!.Job);
    }
    [Fact]
    public async Task ReplacedFolderCannotInheritEarlierDestinationCoverage()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var job=Complete(f,permit,artifact);var health=new GoogleSheetsHealthService(f.Ledger.Business.Store);
        var replacement=await f.Settings.CreateFolderAsync(f.Ledger.Business.Owner,permit.Policy.ConnectionId,permit.Policy.Revision);await f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,true,replacement.Revision));
        var current=health.Current(job.Selection);Assert.Equal("Never completed",current.Status);Assert.Null(current.LastSuccess);Assert.Null(current.ExportedWatermark);Assert.Single(new GoogleSheetsDeliveries(f.Ledger.Business.Store).History(f.Ledger.Business.Owner));
    }
    [Fact]
    public async Task ASuccessStatusWithoutItsCreationAndPublicationProofCannotAdvertiseCoverage()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);string now=DateTimeOffset.UtcNow.ToString("O");
        var attempt=new GoogleSheetsAttempt(1,Guid.NewGuid().ToString("N"),job.Id,job.BusinessId,store.InstanceId,permit.Policy.Revision,"Succeeded",now,now,null,"fictional-file",now);
        store.Transaction(db=>{GoogleSheetsAttemptRecords.Insert(db,attempt);return true;});Assert.Throws<InvalidDataException>(()=>new GoogleSheetsHealthService(store).Current(job.Selection));
    }
    private static GoogleSheetsExportJob Complete(GoogleOutputSettingsTests.Fixture f,GoogleOutputPermit permit,ReadableGenerationArtifact artifact)
    {
        var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);var deliveries=new GoogleSheetsDeliveries(store);var attempt=deliveries.Begin(permit,job);var creation=new GoogleSheetsCreationCheckpoints(store);
        creation.RecordFile(job,creation.BeginCreate(permit,job,creation.Prepare(permit,job)),"fictional-file-"+job.Id);var publication=new GoogleSheetsPublicationCheckpoints(store).Prepare(permit,job,attempt);
        deliveries.Complete(permit,job,attempt,new(publication.FileId,job.GenerationId,job.ManifestSha256,job.SourceWatermark,DateTimeOffset.UtcNow.ToString("O")));return job;
    }
}
