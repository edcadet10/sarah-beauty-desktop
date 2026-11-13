using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsQueuePlanningTests
{
    private sealed class Clock:TimeProvider
    {
        internal DateTimeOffset Now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>Now;
    }
    [Fact]
    public async Task NewPlanAttachesOneJobAndReopensItWhileLaterChangesCoalesce()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);
        var profile=new GoogleSheetsExportProfiles(store).Save(f.Ledger.Business.Owner,new(job.Selection,true,15,0));var clock=new Clock();var plan=new GoogleSheetsQueuePlanning(store,clock);var decision=plan.Observe(permit,profile);Assert.True(decision.Ready);Assert.Equal(job,decision.Job);
        var attached=plan.Attach(permit,profile,decision,job);f.Ledger.Post(f.Ledger.Draft(FinancialKind.Receipt,12345));long watermark=f.Watermark;f.Ledger.Business.Owner.Lock();
        var reopened=new GoogleSheetsQueuePlanning(new BusinessStore(store.DatabasePath),clock).Observe(permit,profile);Assert.True(reopened.Ready);Assert.Equal(job,reopened.Job);Assert.Equal(watermark,reopened.State.ObservedWatermark);
        Assert.Throws<InvalidDataException>(()=>plan.Attach(permit,profile,attached,job));Assert.Equal(DateTimeOffset.MinValue,f.Ledger.Business.Owner.ExpiresAt);Assert.Equal(watermark,f.Watermark);
    }
    [Fact]
    public async Task FailedBackoffPreservesTheSameJobUntilItsRetryIsDue()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);var profile=new GoogleSheetsExportProfiles(store).Save(f.Ledger.Business.Owner,new(job.Selection,true,1,0));var clock=new Clock();var plan=new GoogleSheetsQueuePlanning(store,clock);
        var started=plan.Observe(permit,profile);var failed=started.State with{Status="Failed",FailureCount=1,FailureCode="GoogleQuotaLimited",NextAttemptAtUtc=clock.Now.AddMinutes(1).ToString("O")};store.Transaction(db=>GoogleSheetsQueueRecords.Put(db,started.State,failed));
        clock.Now=clock.Now.AddSeconds(30);var waiting=plan.Observe(permit,profile);Assert.False(waiting.Ready);Assert.Equal("Failed",waiting.State.Status);Assert.Equal(job,waiting.Job);
        clock.Now=clock.Now.AddSeconds(30);var due=plan.Observe(permit,profile);Assert.True(due.Ready);Assert.Equal(job,due.Job);Assert.Equal(1,due.State.FailureCount);
    }
    [Fact]
    public async Task MatchingVerifiedCoverageAvoidsCaptureAndNewChangesWaitForTheMinimumInterval()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var selection=new SarahBeauty.Business.Exports.ReadableGenerationFiles(store).Load(artifact).Manifest.Selection;
        var profile=new GoogleSheetsExportProfiles(store).Save(f.Ledger.Business.Owner,new(selection,true,15,0));using var snapshot=new SarahBeauty.Business.Exports.ReadableExportSnapshots(store).Capture(permit);
        var generation=SarahBeauty.Business.Exports.ReadableExportGeneration.Build(snapshot,selection);var job=GoogleSheetsHealthTests.Complete(f,permit,new SarahBeauty.Business.Exports.ReadableGenerationFiles(store).Stage(generation));
        var health=new GoogleSheetsHealthService(store).Current(selection);var clock=new Clock{Now=DateTimeOffset.Parse(health.CompletedAtUtc!,System.Globalization.CultureInfo.InvariantCulture).AddSeconds(1)};var plan=new GoogleSheetsQueuePlanning(store,clock);
        var covered=plan.Observe(permit,profile);Assert.False(covered.Ready);Assert.Equal("Succeeded",covered.State.Status);Assert.Equal(job.SourceWatermark,covered.State.DeliveredWatermark);
        f.Ledger.Post(f.Ledger.Draft(FinancialKind.Receipt,12345));var waiting=plan.Observe(permit,profile);Assert.False(waiting.Ready);Assert.Equal("Pending",waiting.State.Status);Assert.Null(waiting.Job);
        clock.Now=clock.Now.AddMinutes(15);var due=plan.Observe(permit,profile);Assert.True(due.Ready);Assert.Equal("Preparing",due.State.Status);Assert.Null(due.Job);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChangedReportDatesPreserveAnUncertainCreateButCanSkipAnUntouchedOldSource(bool uncertain)
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);
        if(uncertain){var creation=new GoogleSheetsCreationCheckpoints(store);creation.BeginCreate(permit,job,creation.Prepare(permit,job));}
        var profile=new GoogleSheetsExportProfiles(store).Save(f.Ledger.Business.Owner,new(job.Selection with{FinancialThrough=new(2027,2,1)},true,15,0));var plan=new GoogleSheetsQueuePlanning(store,new Clock());var decision=plan.Observe(permit,profile);
        Assert.True(decision.Ready);if(uncertain){Assert.Equal(job,decision.Job);Assert.Equal(job.Id,plan.Attach(permit,profile,decision,job).State.JobId);}else{Assert.Null(decision.Job);Assert.Equal("Preparing",decision.State.Status);}
        Assert.Equal(job,Assert.Single(store.Read(db=>GoogleSheetsJobRecords.List(db,profile.BusinessId))));
    }
    [Fact]
    public async Task NoExistingJobCreatesOnlyPreparingStateAndStaleProfileCannotAttachIt()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var selection=new SarahBeauty.Business.Exports.ReadableGenerationFiles(store).Load(artifact).Manifest.Selection;
        var profiles=new GoogleSheetsExportProfiles(store);var profile=profiles.Save(f.Ledger.Business.Owner,new(selection,true,15,0));var plan=new GoogleSheetsQueuePlanning(store,new Clock());var decision=plan.Observe(permit,profile);
        Assert.True(decision.Ready);Assert.Equal("Preparing",decision.State.Status);Assert.Null(decision.Job);Assert.Null(decision.State.JobId);
        var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);profiles.Save(f.Ledger.Business.Owner,new(selection,false,15,profile.Revision));Assert.Throws<GoogleConnectionFailure>(()=>plan.Attach(permit,profile,decision,job));
        Assert.Equal(decision.State,store.Read(db=>GoogleSheetsQueueRecords.Read(db,profile.BusinessId,store.InstanceId)));
    }
}
