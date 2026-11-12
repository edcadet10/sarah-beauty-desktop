using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleArchiveHealthTests
{
    [Fact]
    public async Task CurrentCoverageTracksLaterChangesAndUnknownRunningHistoryWithoutRenewingLockedAccess()
    {
        using var f=await GoogleArchiveDeliveriesTests.Fixture.Create();var health=new GoogleArchiveHealthService(f.Google.Ledger.Business.Store);Assert.Equal("Never completed",health.Current().Status);var delivery=f.Reserve();var attempt=f.Deliveries.Begin(f.Permit,delivery);Assert.Equal("Needs review",health.Current().Status);f.Deliveries.Complete(f.Permit,delivery,attempt,GoogleArchiveDeliveriesTests.Fixture.Result(delivery));Assert.Equal("Succeeded",health.Current().Status);
        long covered=f.Archive.Result!.Manifest.Watermark;f.Google.Ledger.Post(f.Google.Ledger.Draft(FinancialKind.Receipt,100));long watermark=f.Google.Watermark;f.Google.Ledger.Business.Owner.Lock();var expiry=f.Google.Ledger.Business.Owner.ExpiresAt;var pending=health.Current();Assert.Equal("Pending",pending.Status);Assert.Equal(covered,pending.ArchivedWatermark);Assert.Equal(watermark,pending.CurrentWatermark);Assert.Equal(expiry,f.Google.Ledger.Business.Owner.ExpiresAt);Assert.Equal(watermark,f.Google.Watermark);
    }
    [Fact]
    public async Task FailedAndDisabledStatesRetainEarlierVerifiedCoverageAndTimes()
    {
        using var f=await GoogleArchiveDeliveriesTests.Fixture.Create();var delivery=f.Reserve();var success=f.Deliveries.Begin(f.Permit,delivery);f.Deliveries.Complete(f.Permit,delivery,success,GoogleArchiveDeliveriesTests.Fixture.Result(delivery));var health=new GoogleArchiveHealthService(f.Google.Ledger.Business.Store);var prior=health.Current();f.Deliveries.Fail(f.Deliveries.Begin(f.Permit,delivery),new GoogleConnectionFailure("GoogleUnavailable","Fictional fixed failure"));var failed=health.Current();Assert.Equal("Failed",failed.Status);Assert.Equal(prior.LastSuccess,failed.LastSuccess);Assert.Equal(prior.CompletedAtUtc,failed.CompletedAtUtc);
        await f.Google.Settings.SaveAsync(f.Google.Ledger.Business.Owner,new(false,false,f.Permit.Policy.Revision));var disabled=health.Current();Assert.Equal("Disabled",disabled.Status);Assert.Equal(prior.ArchivedWatermark,disabled.ArchivedWatermark);Assert.Equal(prior.CompletedAtUtc,disabled.CompletedAtUtc);
    }
    [Fact]
    public async Task ReplacingTheFolderStartsNewCoverageAndKeepsHistoricalDeliveryRecords()
    {
        using var f=await GoogleArchiveDeliveriesTests.Fixture.Create();var delivery=f.Reserve();f.Deliveries.Complete(f.Permit,delivery,f.Deliveries.Begin(f.Permit,delivery),GoogleArchiveDeliveriesTests.Fixture.Result(delivery));var policy=f.Permit.Policy;var folder=await f.Google.Settings.CreateFolderAsync(f.Google.Ledger.Business.Owner,policy.ConnectionId,policy.Revision);await f.Google.Settings.SaveAsync(f.Google.Ledger.Business.Owner,new(true,false,folder.Revision));var health=new GoogleArchiveHealthService(f.Google.Ledger.Business.Store).Current();Assert.Equal("Never completed",health.Status);Assert.Null(health.LastSuccess);Assert.Single(f.Deliveries.History(f.Google.Ledger.Business.Owner));
    }
    [Fact]
    public async Task AChangedInstanceDoesNotCountCopiedCloudHistoryAsCurrentCoverage()
    {
        using var f=await GoogleArchiveDeliveriesTests.Fixture.Create();var delivery=f.Reserve();f.Deliveries.Complete(f.Permit,delivery,f.Deliveries.Begin(f.Permit,delivery),GoogleArchiveDeliveriesTests.Fixture.Result(delivery));f.Google.Ledger.Business.Files.Execute($"UPDATE AppMetadata SET Value='{Guid.NewGuid():N}' WHERE Key='WritableInstanceId';");var store=new BusinessStore(f.Google.Ledger.Business.Files.DatabasePath);var health=new GoogleArchiveHealthService(store).Current();Assert.Equal("Not configured",health.Status);Assert.Null(health.LastSuccess);
    }
    [Fact]
    public async Task ActiveWorkerStatusEndsOnCompletionAndDisconnectRequiresPermissionWithoutErasingCoverage()
    {
        using var f=await GoogleArchiveDeliveriesTests.Fixture.Create();var policy=f.Permit.Policy;using var server=new GoogleArchiveTestServer(policy);using var client=new HttpClient(server);var health=new GoogleArchiveHealthService(f.Google.Ledger.Business.Store);var worker=new GoogleArchiveDeliveryWorker(f.Google.Ledger.Business.Store,f.Google.Connections,f.Google.Settings,client){FailurePoint=point=>{if(point=="Remote archive verified")Assert.Equal("Running",health.Current().Status);}};await worker.DeliverCurrentAsync(f.Archive.Id);var completed=health.Current();Assert.Equal("Succeeded",completed.Status);f.Google.Connections.DisconnectLocal(f.Google.Ledger.Business.Owner,policy.ConnectionId);var disconnected=health.Current();Assert.Equal("Permission required",disconnected.Status);Assert.Equal(completed.LastSuccess,disconnected.LastSuccess);
    }
}
