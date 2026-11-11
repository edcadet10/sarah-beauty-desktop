using System.Security.Cryptography;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class LocalBackupTests
{
    private static long Watermark(LedgerFixture f)=>(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
    private static string Destination(LedgerFixture f,string name="fictional-backup.age")=>Path.Combine(f.Business.Files.Folder,name);
    private static async Task<RecoveryMaterial> Configure(LedgerFixture f)
    {
        var key=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);await new RecoveryProfiles(f.Business.Store).RegisterAsync(f.Business.Owner,key.ProtectedIdentity,BusinessFixture.Phrase);return key;
    }

    [Fact]
    public async Task ConfirmedPublicConfigurationIsIdempotentAndStoresNoRecoverySecret()
    {
        using var f=new LedgerFixture();var profiles=new RecoveryProfiles(f.Business.Store);Assert.Null(profiles.Current(f.Business.Owner));var key=await Configure(f);long watermark=Watermark(f);
        var profile=profiles.Current(f.Business.Owner)!;Assert.Equal(key.Recipient,profile.Recipient);Assert.Equal(Convert.ToHexString(SHA256.HashData(key.ProtectedIdentity)),profile.RecoveryFileHash);
        Assert.Equal(profile,await profiles.RegisterAsync(f.Business.Owner,key.ProtectedIdentity,BusinessFixture.Phrase));Assert.Equal(watermark,Watermark(f));
        string json=(string)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key LIKE 'RecoveryProfile/%';")!;
        Assert.False(json.Contains(BusinessFixture.Phrase,StringComparison.Ordinal));Assert.False(json.Contains(Convert.ToBase64String(key.ProtectedIdentity),StringComparison.Ordinal));Assert.False(json.Contains("PRIVATE-KEY",StringComparison.Ordinal));
        var reopened=new BusinessStore(f.Business.Files.DatabasePath);Assert.Equal(profile,new RecoveryProfiles(reopened).Current(f.Business.Owner));
    }

    [Fact]
    public async Task WrongProofAndForeignSessionsCannotRegisterOrStartAnArchive()
    {
        using var f=new LedgerFixture();using var foreign=new BusinessFixture();var profiles=new RecoveryProfiles(f.Business.Store);var key=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);long watermark=Watermark(f);
        await Assert.ThrowsAsync<RecoveryFailure>(()=>profiles.RegisterAsync(f.Business.Owner,key.ProtectedIdentity,"fictional alternate recovery words"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>profiles.RegisterAsync(foreign.Owner,key.ProtectedIdentity,BusinessFixture.Phrase));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new LocalBackups(f.Business.Store).CreateAsync(foreign.Owner,Destination(f)));
        await Assert.ThrowsAsync<ArgumentException>(()=>new LocalBackups(f.Business.Store).CreateAsync(f.Business.Owner,Destination(f)));
        Assert.Null(profiles.Current(f.Business.Owner));Assert.Empty(new LocalBackups(f.Business.Store).History(f.Business.Owner));Assert.Equal(watermark,Watermark(f));
    }

    [Fact]
    public async Task SuccessfulJobPersistsExactCoverageWithoutCreatingAForeverPendingWatermark()
    {
        using var f=new LedgerFixture();await Configure(f);f.Post(f.Draft(FinancialKind.Receipt,10000));long watermark=Watermark(f);var jobs=new LocalBackups(f.Business.Store);
        Assert.Equal("Never completed",jobs.Health(f.Business.Owner).Status);var saved=await jobs.CreateAsync(f.Business.Owner,Destination(f));
        Assert.Equal("Succeeded",saved.Status);Assert.Equal(watermark,saved.Result!.Manifest.Watermark);Assert.Equal(watermark,Watermark(f));
        Assert.Equal("Succeeded",jobs.Health(f.Business.Owner).Status);Assert.Equal(watermark,jobs.Health(f.Business.Owner).ArchivedWatermark);
        var reopened=new LocalBackups(new BusinessStore(f.Business.Files.DatabasePath));var retained=Assert.Single(reopened.History(f.Business.Owner));Assert.Equal(saved.Result.Sha256,retained.Result!.Sha256);Assert.Equal(saved.Id,retained.Id);
        f.Post(f.Draft(FinancialKind.Expense,2500));Assert.Equal("Pending",reopened.Health(f.Business.Owner).Status);Assert.Equal(watermark,reopened.Health(f.Business.Owner).ArchivedWatermark);Assert.Equal(7500,f.Balance("1010"));
    }

    [Fact]
    public async Task LaterChangesStayPendingAndAJobCanFinishAfterTheHumanSessionLocks()
    {
        using var f=new LedgerFixture();await Configure(f);f.Post(f.Draft(FinancialKind.Receipt,10000));long watermark=Watermark(f);bool running=false;
        var jobs=new LocalBackups(f.Business.Store){FailurePoint=point=>
        {
            if(point=="Database captured"){running=new LocalBackups(f.Business.Store).Health(f.Business.Owner).Status=="Running";f.Post(f.Draft(FinancialKind.Expense,2500));}
            if(point=="Package encrypted")f.Business.Owner.Lock();
        }};
        var saved=await jobs.CreateAsync(f.Business.Owner,Destination(f));Assert.True(running);Assert.Equal("Succeeded",saved.Status);Assert.Equal(watermark,saved.Result!.Manifest.Watermark);
        Assert.Throws<UnauthorizedAccessException>(()=>jobs.History(f.Business.Owner));var owner=new SarahBeauty.Business.Security.OwnerAccess(f.Business.Store).SignIn(f.Business.Owner.OwnerId,BusinessFixture.Phrase);
        var health=jobs.Health(owner);Assert.Equal("Pending",health.Status);Assert.True(health.CurrentWatermark>health.ArchivedWatermark);
        Assert.Equal(7500,new LedgerReports(f.Business.Store).TrialBalance(owner,DateOnly.MinValue,DateOnly.MaxValue).Single(r=>r.Account.Code=="1010").Ending);
    }

    [Fact]
    public async Task FailedAttemptPreservesLastSuccessAndRecordsOnlyAFixedFailureCode()
    {
        using var f=new LedgerFixture();await Configure(f);var jobs=new LocalBackups(f.Business.Store);var saved=await jobs.CreateAsync(f.Business.Owner,Destination(f));long watermark=Watermark(f);
        var broken=new LocalBackups(f.Business.Store){FailurePoint=point=>{if(point=="Package created")throw new IOException("Fictional private failure details");}};
        await Assert.ThrowsAsync<IOException>(()=>broken.CreateAsync(f.Business.Owner,Destination(f,"fictional-failed.age")));
        var health=jobs.Health(f.Business.Owner);Assert.Equal("Failed",health.Status);Assert.Equal(saved.Id,health.LastSuccess!.Id);Assert.Equal("LocalFileUnavailable",health.LastAttempt!.FailureCode);Assert.Null(health.LastAttempt.Result);Assert.Equal(watermark,Watermark(f));Assert.False(File.Exists(Destination(f,"fictional-failed.age")));
    }

    [Fact]
    public async Task CancelledJobRecordsCancellationWithoutPublishingAnArchive()
    {
        using var f=new LedgerFixture();await Configure(f);using var cancellation=new CancellationTokenSource();
        var jobs=new LocalBackups(f.Business.Store){FailurePoint=point=>{if(point=="Package created")cancellation.Cancel();}};
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>jobs.CreateAsync(f.Business.Owner,Destination(f),cancellation.Token));
        var failed=Assert.Single(jobs.History(f.Business.Owner));Assert.Equal("Cancelled",failed.Status);Assert.Equal("Cancelled",failed.FailureCode);Assert.Null(failed.Result);Assert.False(File.Exists(Destination(f)));
    }

    [Fact]
    public async Task PublishedFileWithInterruptedHistoryNeedsReviewRatherThanClaimingSuccessOrActiveExecution()
    {
        using var f=new LedgerFixture();await Configure(f);var jobs=new LocalBackups(f.Business.Store){FailurePoint=point=>{if(point=="History completion")throw new IOException("Fictional unavailable history");}};
        var error=await Assert.ThrowsAsync<IOException>(()=>jobs.CreateAsync(f.Business.Owner,Destination(f)));Assert.Contains("was created",error.Message);Assert.True(File.Exists(Destination(f)));
        Assert.Equal("Running",Assert.Single(jobs.History(f.Business.Owner)).Status);Assert.Equal("Needs review",new LocalBackups(new BusinessStore(f.Business.Files.DatabasePath)).Health(f.Business.Owner).Status);Assert.Null(jobs.Health(f.Business.Owner).LastSuccess);
    }
}
