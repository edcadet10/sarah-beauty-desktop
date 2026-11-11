using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class AutomaticBackupQueueTests
{
    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>Now;
        internal void Advance(int minutes)=>Now=Now.AddMinutes(minutes);
    }
    private static async Task<BackupSchedule> Enable(LedgerFixture f)
    {
        var key=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);await new RecoveryProfiles(f.Business.Store).RegisterAsync(f.Business.Owner,key.ProtectedIdentity,BusinessFixture.Phrase);
        return new BackupSchedules(f.Business.Store).Save(f.Business.Owner,new(true,30,f.Business.Files.Folder));
    }
    private static long Watermark(LedgerFixture f)=>(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;

    [Fact]
    public async Task UnconfiguredOrDisabledConsentStartsNoJobAndCreatesNoLease()
    {
        using var f=new LedgerFixture();var worker=new AutomaticBackups(f.Business.Store);Assert.Equal("Not authorized",(await worker.RunDueAsync()).Status);
        Assert.False(File.Exists(Path.Combine(f.Business.Files.Folder,"automatic-archive.lock")));Assert.Null(worker.Current(f.Business.Owner));
        var schedule=await Enable(f);new BackupSchedules(f.Business.Store).Save(f.Business.Owner,new(false,30,schedule.Directory,schedule.Revision));
        Assert.Equal("Not authorized",(await worker.RunDueAsync()).Status);Assert.Empty(new LocalBackups(f.Business.Store).History(f.Business.Owner));
    }

    [Fact]
    public async Task RepeatedChangesCoalesceAndPendingWorkSurvivesRestartUntilTheInterval()
    {
        using var f=new LedgerFixture();await Enable(f);var clock=new Clock();var worker=new AutomaticBackups(f.Business.Store,clock);var first=await worker.RunDueAsync();
        Assert.Equal("Succeeded",first.Status);Assert.NotNull(first.CompletedJob);long captured=first.State!.ArchivedWatermark!.Value;
        f.Post(f.Draft(FinancialKind.Receipt,1000));f.Post(f.Draft(FinancialKind.Expense,100));long pending=Watermark(f);
        var reopened=new AutomaticBackups(new BusinessStore(f.Business.Files.DatabasePath),clock);var waiting=await reopened.RunDueAsync();Assert.Equal("Pending",waiting.Status);Assert.Equal(pending,waiting.State!.PendingWatermark);Assert.Equal(captured,waiting.State.ArchivedWatermark);Assert.Single(new LocalBackups(f.Business.Store).History(f.Business.Owner));
        clock.Advance(30);var completed=await reopened.RunDueAsync();Assert.Equal("Succeeded",completed.Status);Assert.Equal(pending,completed.State!.ArchivedWatermark);Assert.Equal(pending,Watermark(f));Assert.Equal(2,new LocalBackups(f.Business.Store).History(f.Business.Owner).Count);
        clock.Advance(60);Assert.Null((await reopened.RunDueAsync()).CompletedJob);Assert.Equal(2,new LocalBackups(f.Business.Store).History(f.Business.Owner).Count);
    }

    [Fact]
    public async Task WorkerDoesNotRenewALockedSessionAndLaterCaptureChangesRemainPending()
    {
        using var f=new LedgerFixture();await Enable(f);var clock=new Clock();var worker=new AutomaticBackups(f.Business.Store,clock){FailurePoint=point=>{if(point=="Database captured")f.Post(f.Draft(FinancialKind.Receipt,2500));}};
        var result=await worker.RunDueAsync();Assert.Equal("Pending",result.Status);Assert.True(result.State!.PendingWatermark>result.State.ArchivedWatermark);
        f.Business.Owner.Lock();var expires=f.Business.Owner.ExpiresAt;clock.Advance(30);worker.FailurePoint=null;Assert.Equal("Succeeded",(await worker.RunDueAsync()).Status);Assert.Equal(expires,f.Business.Owner.ExpiresAt);
        Assert.Throws<UnauthorizedAccessException>(()=>worker.Current(f.Business.Owner));
    }

    [Fact]
    public async Task FailedWorkBacksOffAcrossRestartAndRetriesWithoutChangingBusinessWatermark()
    {
        using var f=new LedgerFixture();await Enable(f);var clock=new Clock();long watermark=Watermark(f);
        var broken=new AutomaticBackups(f.Business.Store,clock){FailurePoint=point=>{if(point=="Package created")throw new IOException("Fictional private diagnostic");}};
        await Assert.ThrowsAsync<IOException>(()=>broken.RunDueAsync());var failed=broken.Current(f.Business.Owner)!;Assert.Equal("Failed",failed.Status);Assert.Equal(1,failed.FailureCount);Assert.Equal("LocalFileUnavailable",failed.FailureCode);
        var restarted=new AutomaticBackups(new BusinessStore(f.Business.Files.DatabasePath),clock);Assert.Equal("Failed",(await restarted.RunDueAsync()).Status);Assert.Single(new LocalBackups(f.Business.Store).History(f.Business.Owner));
        clock.Advance(1);await Assert.ThrowsAsync<IOException>(()=>broken.RunDueAsync());Assert.Equal(2,broken.Current(f.Business.Owner)!.FailureCount);clock.Advance(1);Assert.Null((await restarted.RunDueAsync()).CompletedJob);
        clock.Advance(1);var success=await restarted.RunDueAsync();Assert.Equal("Succeeded",success.Status);Assert.Equal(0,success.State!.FailureCount);Assert.Equal(watermark,Watermark(f));Assert.Equal(3,new LocalBackups(f.Business.Store).History(f.Business.Owner).Count);
    }

    [Fact]
    public async Task ExclusiveFileLeasePreventsASecondWorkerFromStarting()
    {
        using var f=new LedgerFixture();await Enable(f);using(var lease=AutomaticArchiveLease.TryAcquire(f.Business.Files.DatabasePath))
        {
            Assert.NotNull(lease);var busy=await new AutomaticBackups(new BusinessStore(f.Business.Files.DatabasePath)).RunDueAsync();Assert.Equal("Busy",busy.Status);Assert.Null(busy.State);Assert.Empty(new LocalBackups(f.Business.Store).History(f.Business.Owner));
        }
        Assert.Equal("Succeeded",(await new AutomaticBackups(f.Business.Store).RunDueAsync()).Status);
    }

    [Fact]
    public async Task InterruptedPublishedAttemptIsPreservedAndNotAdvertisedAsVerifiedCompletion()
    {
        using var f=new LedgerFixture();await Enable(f);var worker=new AutomaticBackups(f.Business.Store){FailurePoint=point=>{if(point=="History completion")throw new IOException("Fictional unavailable job write");}};
        await Assert.ThrowsAsync<IOException>(()=>worker.RunDueAsync());var saved=Assert.Single(new LocalBackups(f.Business.Store).History(f.Business.Owner));Assert.True(File.Exists(saved.Destination));Assert.Null(saved.Result);
        var clock=new Clock();clock.Advance(2);var restarted=new AutomaticBackups(new BusinessStore(f.Business.Files.DatabasePath),clock);Assert.Equal("Succeeded",(await restarted.RunDueAsync()).Status);
        var history=new LocalBackups(f.Business.Store).History(f.Business.Owner);var interrupted=history.Single(j=>j.Id==saved.Id);Assert.Equal("Interrupted",interrupted.Status);Assert.Equal("CompletionUnconfirmed",interrupted.FailureCode);Assert.Null(interrupted.Result);Assert.True(File.Exists(saved.Destination));Assert.Single(history,j=>j.Status=="Succeeded");
    }

    [Fact]
    public async Task CancellationKeepsPendingWorkAndReleasesTheWorkerLease()
    {
        using var f=new LedgerFixture();await Enable(f);using var cancellation=new CancellationTokenSource();var clock=new Clock();
        var worker=new AutomaticBackups(f.Business.Store,clock){FailurePoint=point=>{if(point=="Package created")cancellation.Cancel();}};
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>worker.RunDueAsync(cancellation.Token));Assert.Equal("Cancelled",worker.Current(f.Business.Owner)!.Status);Assert.Null(worker.Current(f.Business.Owner)!.ArchivedWatermark);
        using var available=AutomaticArchiveLease.TryAcquire(f.Business.Files.DatabasePath);Assert.NotNull(available);Assert.Single(new LocalBackups(f.Business.Store).History(f.Business.Owner));
    }

    [Fact]
    public async Task ChangingConsentDoesNotLeaveAnOldRetryTimeOrEnableTheCopiedInstance()
    {
        using var f=new LedgerFixture();var schedule=await Enable(f);var clock=new Clock();var worker=new AutomaticBackups(f.Business.Store,clock);await worker.RunDueAsync();
        new BackupSchedules(f.Business.Store).Save(f.Business.Owner,new(true,1,schedule.Directory,schedule.Revision));clock.Advance(2);Assert.Equal("Succeeded",(await worker.RunDueAsync()).Status);Assert.Equal(schedule.Revision+1,worker.Current(f.Business.Owner)!.ScheduleRevision);
        f.Business.Files.Execute($"UPDATE AppMetadata SET Value='{Guid.NewGuid():N}' WHERE Key='WritableInstanceId';");var store=new BusinessStore(f.Business.Files.DatabasePath);Assert.Equal("Not authorized",(await new AutomaticBackups(store,clock).RunDueAsync()).Status);
    }
}
