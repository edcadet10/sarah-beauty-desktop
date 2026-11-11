using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class BackupScheduleTests
{
    private static async Task<(BackupSchedules Schedules,BackupPermit Permit)> Enabled(LedgerFixture f)
    {
        var key=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);await new RecoveryProfiles(f.Business.Store).RegisterAsync(f.Business.Owner,key.ProtectedIdentity,BusinessFixture.Phrase);
        var schedules=new BackupSchedules(f.Business.Store);schedules.Save(f.Business.Owner,new(true,30,f.Business.Files.Folder+Path.DirectorySeparatorChar));return(schedules,schedules.Authorize());
    }
    private static long Watermark(LedgerFixture f)=>(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;

    [Fact]
    public async Task ScheduleConsentIsInstanceBoundRevisionedAndIdempotent()
    {
        using var f=new LedgerFixture();var enabled=await Enabled(f);var prior=enabled.Schedules.Current(f.Business.Owner)!;long watermark=Watermark(f);
        Assert.Equal(f.Business.Store.InstanceId,prior.InstanceId);Assert.Equal(f.Business.Files.Folder,prior.Directory);Assert.Equal(prior,enabled.Schedules.Save(f.Business.Owner,new(true,30,prior.Directory,prior.Revision)));Assert.Equal(watermark,Watermark(f));
        var changed=enabled.Schedules.Save(f.Business.Owner,new(true,60,prior.Directory,prior.Revision));Assert.Equal(prior.Revision+1,changed.Revision);Assert.Equal(watermark+1,Watermark(f));
        Assert.Throws<ArgumentException>(()=>enabled.Schedules.Save(f.Business.Owner,new(false,30,prior.Directory,prior.Revision)));Assert.Equal(changed,new BackupSchedules(new BusinessStore(f.Business.Files.DatabasePath)).Current(f.Business.Owner));
    }

    [Fact]
    public async Task AutomaticArchiveRunsWithALockedHumanSessionWithoutRenewingItOrChangingBooks()
    {
        using var f=new LedgerFixture();f.Post(f.Draft(FinancialKind.Receipt,10000));var enabled=await Enabled(f);long watermark=Watermark(f);f.Business.Owner.Lock();var expiration=f.Business.Owner.ExpiresAt;
        string destination=Path.Combine(f.Business.Files.Folder,"fictional-automatic.age");var created=await new FullArchives(f.Business.Store).CreateAutomaticAsync(enabled.Permit,destination);
        Assert.Equal(watermark,created.Manifest.Watermark);Assert.Equal(1,created.Manifest.Facts.MovementCount);Assert.Equal(expiration,f.Business.Owner.ExpiresAt);Assert.Equal(watermark,Watermark(f));Assert.True(File.Exists(destination));
        Assert.Throws<UnauthorizedAccessException>(()=>f.Book.Posted(f.Business.Owner));var fresh=new OwnerAccess(f.Business.Store).SignIn(f.Business.Owner.OwnerId,BusinessFixture.Phrase);Assert.Single(f.Book.Posted(fresh));
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("revision")]
    [InlineData("owner")]
    [InlineData("recipient")]
    public async Task ChangedConsentCannotPublishAnAlreadyEncryptedAutomaticArchive(string change)
    {
        using var f=new LedgerFixture();var enabled=await Enabled(f);RecoveryMaterial? replacement=change=="recipient"?await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase):null;
        var archives=new FullArchives(f.Business.Store){FailurePoint=point=>
        {
            if(point!="Package encrypted")return;var current=enabled.Schedules.Current(f.Business.Owner)!;
            if(change=="disabled")enabled.Schedules.Save(f.Business.Owner,new(false,current.IntervalMinutes,current.Directory,current.Revision));
            if(change=="revision")enabled.Schedules.Save(f.Business.Owner,new(true,60,current.Directory,current.Revision));
            if(change=="owner")f.Business.Store.Transaction(db=>db.Execute("UPDATE Owners SET Active=0 WHERE Id=$id;",("$id",f.Business.Owner.OwnerId)));
            if(change=="recipient")new RecoveryProfiles(f.Business.Store).RegisterAsync(f.Business.Owner,replacement!.ProtectedIdentity,BusinessFixture.Phrase).GetAwaiter().GetResult();
        }};
        string destination=Path.Combine(f.Business.Files.Folder,"fictional-revoked.age");await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>archives.CreateAutomaticAsync(enabled.Permit,destination));Assert.False(File.Exists(destination));
        Assert.Empty(Directory.EnumerateDirectories(f.Business.Files.Folder,".sarah-recovery-*"));Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(f.Business.Files.Folder,"RecoveryStaging")));
    }

    [Fact]
    public async Task AutomaticAuthorityCannotChooseAnotherFolderOrFollowAChangedInstance()
    {
        using var f=new LedgerFixture();using var other=new TestStore();var enabled=await Enabled(f);
        await Assert.ThrowsAsync<ArgumentException>(()=>new FullArchives(f.Business.Store).CreateAutomaticAsync(enabled.Permit,Path.Combine(other.Folder,"must-not-publish.age")));Assert.Empty(Directory.EnumerateFiles(other.Folder));
        f.Business.Files.Execute($"UPDATE AppMetadata SET Value='{Guid.NewGuid():N}' WHERE Key='WritableInstanceId';");var reopened=new BusinessStore(f.Business.Files.DatabasePath);
        Assert.Throws<UnauthorizedAccessException>(()=>new BackupSchedules(reopened).Authorize());await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new FullArchives(reopened).CreateAutomaticAsync(enabled.Permit,Path.Combine(f.Business.Files.Folder,"must-not-publish.age")));Assert.False(File.Exists(Path.Combine(f.Business.Files.Folder,"must-not-publish.age")));
    }

    [Fact]
    public void InvalidSetupForeignSessionsAndMissingRecoveryCannotEnableARecurringArchive()
    {
        using var f=new LedgerFixture();using var foreign=new BusinessFixture();var schedules=new BackupSchedules(f.Business.Store);Assert.Null(schedules.Current(f.Business.Owner));
        Assert.Throws<ArgumentException>(()=>schedules.Save(f.Business.Owner,new(true,30,f.Business.Files.Folder)));Assert.Throws<ArgumentException>(()=>schedules.Save(f.Business.Owner,new(true,0,f.Business.Files.Folder)));
        Assert.Throws<ArgumentException>(()=>schedules.Save(f.Business.Owner,new(true,10081,f.Business.Files.Folder)));Assert.Throws<ArgumentException>(()=>schedules.Save(f.Business.Owner,new(true,30,Path.Combine(f.Business.Files.Folder,"missing"))));
        Assert.Throws<UnauthorizedAccessException>(()=>schedules.Save(foreign.Owner,new(false,30,"")));Assert.Null(schedules.Current(f.Business.Owner));Assert.Throws<UnauthorizedAccessException>(()=>schedules.Authorize());
    }

    [Fact]
    public async Task PermissionCopiedFromAnotherDatabaseInstanceCannotBeReused()
    {
        using var f=new LedgerFixture();using var copied=new TestStore();var enabled=await Enabled(f);DatabaseBackup.CreateVerifiedSnapshotAt(f.Business.Files.DatabasePath,copied.DatabasePath);
        copied.Execute($"UPDATE AppMetadata SET Value='{Guid.NewGuid():N}' WHERE Key='WritableInstanceId';");var store=new BusinessStore(copied.DatabasePath);var owner=new OwnerAccess(store).SignIn(f.Business.Owner.OwnerId,BusinessFixture.Phrase);
        Assert.Null(new BackupSchedules(store).Current(owner));Assert.Throws<UnauthorizedAccessException>(()=>new BackupSchedules(store).Authorize());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new FullArchives(store).CreateAutomaticAsync(enabled.Permit,Path.Combine(f.Business.Files.Folder,"foreign-automatic.age")));Assert.False(File.Exists(Path.Combine(f.Business.Files.Folder,"foreign-automatic.age")));
    }
}
