using System.Globalization;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsQueueTests
{
    private sealed class Clock:TimeProvider
    {
        internal DateTimeOffset Now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>Now;
    }
    private sealed class Fixture:IDisposable
    {
        internal readonly GoogleOutputSettingsTests.Fixture Google;
        internal readonly GoogleOutputPermit Permit;
        internal readonly GoogleSheetsExportProfile Profile;
        internal readonly GoogleSheetsTestServer Server;
        internal readonly HttpClient Client;
        internal readonly Clock Time=new();
        internal readonly GoogleSheetsDeliveryWorker Worker;
        internal readonly GoogleSheetsQueue Queue;
        internal BusinessStore Store=>Google.Ledger.Business.Store;
        private Fixture(GoogleOutputSettingsTests.Fixture google,GoogleOutputPermit permit,GoogleSheetsExportProfile profile)
        {
            Google=google;Permit=permit;Profile=profile;Server=new(Store,permit);Client=new(Server);Worker=new(Store,Google.Connections,Google.Settings,Client){WaitForRequest=token=>{token.ThrowIfCancellationRequested();return Task.CompletedTask;}};
            Queue=new(Store,Google.Settings,Worker,Time);
        }
        internal static async Task<Fixture> Create(bool enabled=true)
        {
            var google=new GoogleOutputSettingsTests.Fixture();try
            {
                var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(google);var selection=new ReadableGenerationFiles(google.Ledger.Business.Store).Load(artifact).Manifest.Selection;
                var profile=new GoogleSheetsExportProfiles(google.Ledger.Business.Store).Save(google.Ledger.Business.Owner,new(selection,enabled,1,0));return new(google,permit,profile);
            }
            catch{google.Dispose();throw;}
        }
        internal GoogleSheetsQueueState State()=>Store.Read(db=>GoogleSheetsQueueRecords.Read(db,Permit.Policy.BusinessId,Store.InstanceId))!;
        public void Dispose(){Client.Dispose();Google.Dispose();}
    }
    [Fact]
    public async Task DisabledSheetsConsentReportsOffWithoutCapturingEvenWhenAutomaticChoiceIsSaved()
    {
        using var f=await Fixture.Create();await f.Google.Settings.SaveAsync(f.Google.Ledger.Business.Owner,new(false,false,f.Permit.Policy.Revision));
        Assert.Equal("Sheets export disabled or not configured",(await f.Queue.RunDueAsync()).Status);Assert.Equal(0,f.Server.Calls);Assert.Empty(f.Store.Read(db=>GoogleSheetsJobRecords.List(db,f.Permit.Policy.BusinessId)));
    }
    [Fact]
    public async Task DisabledAutomaticChoiceMakesNoHttpOrJobEvenWithManualSheetsPermission()
    {
        using var f=await Fixture.Create(false);Assert.Contains("disabled",(await f.Queue.RunDueAsync()).Status);Assert.Equal(0,f.Server.Calls);
        Assert.Empty(f.Store.Read(db=>GoogleSheetsJobRecords.List(db,f.Permit.Policy.BusinessId)));Assert.True(f.Google.Settings.Authorize(GoogleOutputKind.SheetsExport).Policy.SheetsExportEnabled);
    }
    [Fact]
    public async Task DueCaptureDeliversOnceAndCoveredPollsDoNotWriteOrRenewTheLockedOwner()
    {
        using var f=await Fixture.Create();long watermark=f.Google.Watermark;f.Google.Ledger.Business.Owner.Lock();var result=await f.Queue.RunDueAsync();Assert.Equal("Succeeded",result.Status);Assert.Equal(watermark,result.State!.DeliveredWatermark);int calls=f.Server.Calls;
        var covered=await f.Queue.RunDueAsync();Assert.Equal("Succeeded",covered.Status);Assert.Null(covered.Attempt);Assert.Equal(calls,f.Server.Calls);Assert.Equal(1,f.Server.Creates);
        Assert.Equal(watermark,f.Google.Watermark);Assert.Equal(DateTimeOffset.MinValue,f.Google.Ledger.Business.Owner.ExpiresAt);
    }
    [Fact]
    public async Task FailedCreatePersistsBackoffAndReopensTheSamePendingJob()
    {
        using var f=await Fixture.Create();f.Server.LostCreate=true;await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>f.Queue.RunDueAsync());var failed=f.State();Assert.Equal("Failed",failed.Status);Assert.NotNull(failed.JobId);int calls=f.Server.Calls;
        Assert.Equal("Failed",(await f.Queue.RunDueAsync()).Status);Assert.Equal(calls,f.Server.Calls);f.Time.Now=f.Time.Now.AddMinutes(1);var reopened=new BusinessStore(f.Store.DatabasePath);
        var worker=new GoogleSheetsDeliveryWorker(reopened,f.Google.Connections,new(reopened),f.Client){WaitForRequest=token=>{token.ThrowIfCancellationRequested();return Task.CompletedTask;}};
        var result=await new GoogleSheetsQueue(reopened,new(reopened),worker,f.Time).RunDueAsync();Assert.Equal("Succeeded",result.Status);Assert.Equal(failed.JobId,result.Attempt!.JobId);Assert.Equal(1,f.Server.Creates);
    }
    [Fact]
    public async Task ChangesAfterCaptureStayPendingAndWaitWithoutCreatingAnotherWorkbook()
    {
        using var f=await Fixture.Create();f.Worker.FailurePoint=point=>{if(point=="Source values sent")f.Google.Ledger.Post(f.Google.Ledger.Draft(FinancialKind.Receipt,12345));};
        var result=await f.Queue.RunDueAsync();Assert.Equal("Pending",result.Status);Assert.True(result.State!.DeliveredWatermark<result.State.ObservedWatermark);Assert.Equal("Succeeded",result.Attempt!.Status);int calls=f.Server.Calls;
        Assert.Equal("Pending",(await f.Queue.RunDueAsync()).Status);Assert.Equal(calls,f.Server.Calls);Assert.Equal(1,f.Server.Creates);
    }
    [Theory]
    [InlineData("source")]
    [InlineData("final writer")]
    public async Task WithdrawingAutomaticChoiceStopsScheduledWorkAndLeavesManualPermissionAvailable(string point)
    {
        using var f=await Fixture.Create();var profiles=new GoogleSheetsExportProfiles(f.Store);
        void Disable()=>profiles.Save(f.Google.Ledger.Business.Owner,new(f.Profile.Selection,false,1,f.Profile.Revision));
        f.Worker.FailurePoint=stage=>
        {
            if(point=="source"&&stage=="Source values sent")Disable();
            if(point=="final writer"&&stage=="Remote readable export verified")f.Store.BeforeTransactionReservation=()=>{f.Store.BeforeTransactionReservation=null;Disable();};
        };
        Assert.Equal("GoogleSheetsSchedulePermissionUnavailable",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>f.Queue.RunDueAsync())).Code);
        Assert.Equal("Disabled",f.State().Status);Assert.True(f.Google.Settings.Authorize(GoogleOutputKind.SheetsExport).Policy.SheetsExportEnabled);Assert.DoesNotContain(f.Store.Read(db=>GoogleSheetsAttemptRecords.List(db,f.Permit.Policy.BusinessId)),attempt=>attempt.Status=="Succeeded");
        f.Worker.FailurePoint=null;var job=Assert.Single(f.Store.Read(db=>GoogleSheetsJobRecords.List(db,f.Permit.Policy.BusinessId)));Assert.Equal("Succeeded",(await f.Worker.RetryCurrentAsync(job.Id)).Status);
    }
    [Fact]
    public async Task QuotaBackoffDoublesToSixtyMinutesWhileKeepingOneRemoteJob()
    {
        using var f=await Fixture.Create();f.Server.Quota=true;
        foreach(int minutes in new[]{1,2,4,8,16,32,60,60})
        {
            Assert.Equal("GoogleQuotaLimited",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>f.Queue.RunDueAsync())).Code);var state=f.State();var next=DateTimeOffset.ParseExact(state.NextAttemptAtUtc!,"O",CultureInfo.InvariantCulture);
            Assert.Equal(TimeSpan.FromMinutes(minutes),next-f.Time.Now);f.Time.Now=next;
        }
        Assert.Equal(1,f.Server.Creates);Assert.Single(f.Store.Read(db=>GoogleSheetsJobRecords.List(db,f.Permit.Policy.BusinessId)));f.Server.Quota=false;Assert.Equal("Succeeded",(await f.Queue.RunDueAsync()).Status);Assert.Equal(0,f.State().FailureCount);
    }
    [Fact]
    public async Task QueueLeasePreventsCaptureAndHttp()
    {
        using var f=await Fixture.Create();using var lease=GoogleSheetsDeliveryLease.TryQueue(f.Store);Assert.NotNull(lease);Assert.Equal("Busy",(await f.Queue.RunDueAsync()).Status);Assert.Equal(0,f.Server.Calls);Assert.Empty(f.Store.Read(db=>GoogleSheetsJobRecords.List(db,f.Permit.Policy.BusinessId)));
    }
}
