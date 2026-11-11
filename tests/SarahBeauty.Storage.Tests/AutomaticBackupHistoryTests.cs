using System.Text.Json;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class AutomaticBackupHistoryTests
{
    private static async Task<BackupPermit> Enable(LedgerFixture f)
    {
        var key=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);await new RecoveryProfiles(f.Business.Store).RegisterAsync(f.Business.Owner,key.ProtectedIdentity,BusinessFixture.Phrase);
        var schedules=new BackupSchedules(f.Business.Store);schedules.Save(f.Business.Owner,new(true,30,f.Business.Files.Folder));return schedules.Authorize();
    }
    private static long Watermark(LedgerFixture f)=>(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;

    [Fact]
    public async Task AutomaticCompletionPersistsWithLockedHumanAccessAndNoWatermarkLoop()
    {
        using var f=new LedgerFixture();f.Post(f.Draft(FinancialKind.Receipt,12345));var permit=await Enable(f);long watermark=Watermark(f);f.Business.Owner.Lock();var expires=f.Business.Owner.ExpiresAt;
        var saved=await new LocalBackups(f.Business.Store).CreateAutomaticAsync(permit);Assert.Equal("Succeeded",saved.Status);Assert.Equal(permit.Schedule.Id,saved.AutomaticScheduleId);Assert.Equal(permit.Schedule.Revision,saved.AutomaticScheduleRevision);
        Assert.Equal(f.Business.Store.InstanceId,saved.InstanceId);Assert.Equal(permit.Schedule.Directory,Path.GetDirectoryName(saved.Destination));Assert.Equal(watermark,saved.Result!.Manifest.Watermark);Assert.Equal(watermark,Watermark(f));Assert.Equal(expires,f.Business.Owner.ExpiresAt);
        var reopened=new BusinessStore(f.Business.Files.DatabasePath);var owner=new OwnerAccess(reopened).SignIn(f.Business.Owner.OwnerId,BusinessFixture.Phrase);var jobs=new LocalBackups(reopened);
        Assert.Equal(saved.Id,Assert.Single(jobs.History(owner)).Id);Assert.Equal("Succeeded",jobs.Health(owner).Status);Assert.Equal(watermark,jobs.Health(owner).ArchivedWatermark);Assert.True(File.Exists(saved.Destination));
    }

    [Fact]
    public async Task AutomaticCaptureLeavesLaterBusinessChangesPending()
    {
        using var f=new LedgerFixture();var permit=await Enable(f);long watermark=Watermark(f);
        var jobs=new LocalBackups(f.Business.Store){FailurePoint=point=>{if(point=="Database captured")f.Post(f.Draft(FinancialKind.Receipt,2000));}};var saved=await jobs.CreateAutomaticAsync(permit);
        Assert.Equal(watermark,saved.Result!.Manifest.Watermark);Assert.Equal("Pending",jobs.Health(f.Business.Owner).Status);Assert.True(jobs.Health(f.Business.Owner).CurrentWatermark>watermark);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrCancelledAutomaticJobsRetainExactAttemptWithoutPublishing(bool cancel)
    {
        using var f=new LedgerFixture();var permit=await Enable(f);using var cancellation=new CancellationTokenSource();long watermark=Watermark(f);
        var jobs=new LocalBackups(f.Business.Store){FailurePoint=point=>{if(point!="Package created")return;if(cancel)cancellation.Cancel();else throw new IOException("Fictional private diagnostic text");}};
        await Assert.ThrowsAnyAsync<Exception>(()=>jobs.CreateAutomaticAsync(permit,cancellation.Token));var failed=Assert.Single(jobs.History(f.Business.Owner));
        Assert.Equal(cancel?"Cancelled":"Failed",failed.Status);Assert.Equal(cancel?"Cancelled":"LocalFileUnavailable",failed.FailureCode);Assert.Equal(permit.Schedule.Id,failed.AutomaticScheduleId);Assert.Null(failed.Result);Assert.False(File.Exists(failed.Destination));Assert.Equal(watermark,Watermark(f));
        Assert.DoesNotContain("private diagnostic",JsonSerializer.Serialize(failed));
    }

    [Fact]
    public async Task RevokedConsentCreatesNoNewAttemptAndDoesNotUseAnOldPermit()
    {
        using var f=new LedgerFixture();var permit=await Enable(f);new BackupSchedules(f.Business.Store).Save(f.Business.Owner,new(false,30,permit.Schedule.Directory,permit.Schedule.Revision));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new LocalBackups(f.Business.Store).CreateAutomaticAsync(permit));Assert.Empty(new LocalBackups(f.Business.Store).History(f.Business.Owner));
    }

    [Fact]
    public async Task HistoricalInstanceAttemptsRemainVisibleWithoutClaimingCurrentCoverageOrActivity()
    {
        using var f=new LedgerFixture();var permit=await Enable(f);var jobs=new LocalBackups(f.Business.Store);var saved=await jobs.CreateAutomaticAsync(permit);
        var historical=saved with{InstanceId=Guid.NewGuid().ToString("N")};var running=saved with{Id=Guid.NewGuid().ToString(),InstanceId=null,AutomaticScheduleId=null,AutomaticScheduleRevision=null,Status="Running",Result=null,FinishedAtUtc=null};
        f.Business.Store.Transaction(db=>{db.Execute("UPDATE AppMetadata SET Value=$json WHERE Key=$key;",("$json",JsonSerializer.Serialize(historical)),("$key",BackupJobRecords.Key(saved.Id)));BackupJobRecords.Insert(db,running);return true;});
        Assert.Equal(2,jobs.History(f.Business.Owner).Count);var health=jobs.Health(f.Business.Owner);Assert.Equal("Never completed",health.Status);Assert.Null(health.LastAttempt);Assert.Null(health.ArchivedWatermark);Assert.True(File.Exists(saved.Destination));
    }

    [Fact]
    public void CompletionRejectsChangedImmutableAttemptFacts()
    {
        using var f=new LedgerFixture();var job=new LocalBackupJob(1,Guid.NewGuid().ToString(),f.Business.Owner.BusinessId,"Running","fictional.age",new string('A',64),0,DateTimeOffset.UtcNow.ToString("O"),InstanceId:f.Business.Store.InstanceId);
        f.Business.Store.Transaction(db=>{BackupJobRecords.Insert(db,job);return true;});
        Assert.Throws<InvalidDataException>(()=>f.Business.Store.Transaction(db=>{BackupJobRecords.Complete(db,job with{Status="Failed",RequestedWatermark=1});return true;}));
        Assert.Equal("Running",Assert.Single(new LocalBackups(f.Business.Store).History(f.Business.Owner)).Status);
    }
}
