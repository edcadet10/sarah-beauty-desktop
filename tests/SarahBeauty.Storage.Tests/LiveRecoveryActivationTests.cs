using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Setup;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class LiveRecoveryActivationTests
{
    private const string Phrase="fictional orchard lantern replacement access";
    [Fact]
    public async Task LivePrimaryNeedsCoveredRetirementBeforeRecoveredActivationAndReopensInLiveMode()
    {
        using var f=new ModeRecoveryTests.Fixture(BusinessMode.Live);string root=Path.Combine(f.Files.Folder,"Live");var prepared=await Prepare(f,root);var service=new ActiveBusinessStores(root,BusinessMode.Live);string request=prepared.Copy.Receipt.RequestId;
        Assert.True(Assert.Single(new PreparedStoreCatalog(root,BusinessMode.Live).List()).CanActivate);Assert.False(Assert.Single(new PreparedStoreCatalog(root).List()).CanActivate);await Assert.ThrowsAsync<InvalidOperationException>(()=>new ActiveTestStores(root).ActivateAsync(request,f.Owner.OwnerId,Phrase));await Assert.ThrowsAsync<ArgumentException>(()=>service.ActivateAsync(request,f.Owner.OwnerId,Phrase));Assert.False(File.Exists(Path.Combine(root,"active-live-store.json")));
        await new StoreRetirement(f.Store).RetireAsync(f.Owner,prepared.Job.Id,"Fictional controlled LIVE replacement",BusinessFixture.Phrase);Assert.False(f.Owner.IsActive);var active=await service.ActivateAsync(request,f.Owner.OwnerId,Phrase);Assert.Equal(BusinessMode.Live,active.Store.Mode);Assert.Equal(BusinessMode.Live,active.Owner.Mode);Assert.Equal("LIVE",active.Selection.Mode);Assert.NotEqual(f.Store.InstanceId,active.Store.InstanceId);Assert.Equal(f.Profile,new BusinessProfiles(active.Store).Current(active.Owner));
        Assert.Equal(prepared.Copy.DatabasePath,new LiveBusinesses(root).Open().DatabasePath);Assert.Equal(prepared.Copy.DatabasePath,service.ResolveDatabase());Assert.Equal("Retired",TestStore.Scalar(f.Store.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='WritableInstanceState';"));Assert.Equal(2300L,TestStore.Scalar(f.Store.DatabasePath,"SELECT SUM(DebitCents) FROM JournalLines;"));Assert.Equal(2300L,TestStore.Scalar(active.Store.DatabasePath,"SELECT SUM(CreditCents) FROM JournalLines;"));
        long watermark=Convert.ToInt64(TestStore.Scalar(active.Store.DatabasePath,"SELECT Watermark FROM Business;"));var retry=await service.ActivateAsync(request,f.Owner.OwnerId,Phrase);Assert.Equal(active.Store.InstanceId,retry.Store.InstanceId);Assert.Equal(watermark,TestStore.Scalar(active.Store.DatabasePath,"SELECT Watermark FROM Business;"));Assert.False(File.Exists(Path.Combine(root,"active-test-store.json")));
    }
    [Fact]
    public async Task SelectedRecoveryWithoutAPrimaryFolderStillBlocksCleanCreation()
    {
        using var f=new ModeRecoveryTests.Fixture(BusinessMode.Live);using var target=new TestStore();var prepared=await Prepare(f,target.Folder);byte[] source=File.ReadAllBytes(f.Store.DatabasePath);var active=await new ActiveBusinessStores(target.Folder,BusinessMode.Live).ActivateAsync(prepared.Copy.Receipt.RequestId,f.Owner.OwnerId,Phrase);var businesses=new LiveBusinesses(target.Folder);Assert.True(businesses.Exists);Assert.False(Directory.Exists(Path.Combine(target.Folder,"Primary")));Assert.Equal(active.Store.InstanceId,businesses.Open().InstanceId);Assert.Throws<IOException>(()=>Create(businesses));Assert.Equal(source,File.ReadAllBytes(f.Store.DatabasePath));Assert.Empty(Directory.GetDirectories(target.Folder,".pending-live-*"));
        active.Store.Transaction(db=>db.Execute("UPDATE AppMetadata SET Value=json_set(Value,'$.Mode','TEST') WHERE Key=$key;",("$key","RecoveredStore/"+prepared.Copy.Receipt.RequestId)));await Assert.ThrowsAsync<InvalidDataException>(()=>new ActiveBusinessStores(target.Folder,BusinessMode.Live).ActivateAsync(prepared.Copy.Receipt.RequestId,f.Owner.OwnerId,Phrase));
    }
    [Theory]
    [InlineData("Prepared copy verified",false)][InlineData("Selection published",false)][InlineData("Selection published",true)][InlineData("Selection committed",false)]
    public async Task InterruptedLiveActivationRollsBackOrRequiresReopeningItsCommittedSelection(string point,bool cancel)
    {
        using var f=new ModeRecoveryTests.Fixture(BusinessMode.Live);using var target=new TestStore();var prepared=await Prepare(f,target.Folder);using var cancellation=new CancellationTokenSource();var service=new ActiveBusinessStores(target.Folder,BusinessMode.Live){FailurePoint=step=>{if(step!=point)return;if(cancel)cancellation.Cancel();else throw new IOException("Fictional interrupted LIVE selection");}};
        if(cancel)await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.ActivateAsync(prepared.Copy.Receipt.RequestId,f.Owner.OwnerId,Phrase,cancellation.Token));else await Assert.ThrowsAsync<IOException>(()=>service.ActivateAsync(prepared.Copy.Receipt.RequestId,f.Owner.OwnerId,Phrase));
        bool committed=point=="Selection committed";Assert.Equal(committed?"Active":"Prepared",TestStore.Scalar(prepared.Copy.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='WritableInstanceState';"));Assert.Equal(committed,File.Exists(Path.Combine(target.Folder,"active-live-store.json")));Assert.False(File.Exists(Path.Combine(target.Folder,"active-test-store.json")));if(committed)Assert.Equal(prepared.Copy.DatabasePath,new LiveBusinesses(target.Folder).Open().DatabasePath);
    }
    [Fact]
    public async Task ARecoveredArchiveCannotReplaceAnUnrelatedLiveBusiness()
    {
        using var f=new ModeRecoveryTests.Fixture(BusinessMode.Live);using var target=new TestStore();var current=Create(new(target.Folder));var prepared=await Prepare(f,target.Folder);byte[] before=File.ReadAllBytes(current.Store.DatabasePath);await Assert.ThrowsAsync<ArgumentException>(()=>new ActiveBusinessStores(target.Folder,BusinessMode.Live).ActivateAsync(prepared.Copy.Receipt.RequestId,f.Owner.OwnerId,Phrase));Assert.Equal(before,File.ReadAllBytes(current.Store.DatabasePath));Assert.True(current.Owner.IsActive);Assert.False(File.Exists(Path.Combine(target.Folder,"active-live-store.json")));
    }
    [Fact]
    public void CleanCreationUsesTheSameSelectionLockAndPreservesItsPendingStage()
    {
        using var f=new TestStore();string root=Path.Combine(f.Folder,"Live");FileStream? lease=null;var businesses=new LiveBusinesses(root){BeforePublish=()=>lease=new(Path.Combine(root,".active-live-store.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)};
        try{Assert.Throws<IOException>(()=>Create(businesses));Assert.False(Directory.Exists(Path.Combine(root,"Primary")));Assert.Single(Directory.GetDirectories(root,".pending-live-*"));}finally{lease?.Dispose();}Assert.Equal(BusinessMode.Live,Create(new(root)).Store.Mode);
    }
    [Fact]
    public async Task OlderLiveRecoveryKeepsMissingSetupUnknownButCannotLoseANewlyRecordedProfile()
    {
        using var source=new BusinessFixture();using var legacy=new TestStore();using var target=new TestStore();var older=await StoreInstanceTests.EarlierArchive(source,legacy,38,BusinessMode.Live);using var review=await new ArchiveRestores().StageAsync(older.Archive,legacy.Folder,older.Key.ProtectedIdentity,BusinessFixture.Phrase);
        var prepared=await new RecoveredStores(BusinessMode.Live).PrepareAsync(review,target.Folder,new(source.Owner.OwnerId,"Fictional older LIVE archive review",true),Phrase,Guid.NewGuid().ToString());var active=await new ActiveBusinessStores(target.Folder,BusinessMode.Live).ActivateAsync(prepared.Receipt.RequestId,source.Owner.OwnerId,Phrase);var businesses=new LiveBusinesses(target.Folder);Assert.Equal(active.Store.InstanceId,businesses.Open().InstanceId);Assert.Null(new BusinessProfiles(active.Store).Current(active.Owner));Assert.Equal(0L,TestStore.Scalar(active.Store.DatabasePath,"SELECT count(*) FROM Movements;"));
        var saved=new BusinessProfiles(active.Store).Confirm(active.Owner,new(0,new(){DisplayName=active.Store.BusinessName(active.Owner)},"Fictional initial unknown setup after recovery"),Phrase,Guid.NewGuid().ToString());Assert.Null(saved.Profile.Currency);Assert.Equal(BusinessSetupState.Unconfirmed,saved.Profile.Tax.State);Assert.Equal(active.Store.InstanceId,businesses.Open().InstanceId);
        active.Store.Transaction(db=>db.Execute("DROP TRIGGER BusinessProfileVersions_NoDelete; DELETE FROM BusinessProfileVersions;"));Assert.Throws<InvalidDataException>(()=>businesses.Open());Assert.Equal("TEST",TestStore.Scalar(source.Store.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='StoreMode';"));
    }
    private static async Task<(LocalBackupJob Job,PreparedRecoveredStore Copy)> Prepare(ModeRecoveryTests.Fixture f,string root)
    {
        var key=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);await new RecoveryProfiles(f.Store).RegisterAsync(f.Owner,key.ProtectedIdentity,BusinessFixture.Phrase);var job=await new LocalBackups(f.Store).CreateAsync(f.Owner,Path.Combine(f.Files.Folder,"fictional-live-activation.age"));using var review=await new ArchiveRestores().StageAsync(job.Destination,root,key.ProtectedIdentity,BusinessFixture.Phrase,job.Result);var copy=await new RecoveredStores(BusinessMode.Live).PrepareAsync(review,root,new(f.Owner.OwnerId,"Fictional reviewed LIVE activation",true),Phrase,Guid.NewGuid().ToString());return(job,copy);
    }
    private static CreatedLiveBusiness Create(LiveBusinesses businesses)=>businesses.Create(new(){DisplayName="Fictional separate LIVE business",Currency="USD",TimeZoneId="UTC",BooksStartDate=new DateOnly(2026,11,12),NewBusinessOnlyConfirmed=true},"Alex",BusinessFixture.Phrase,"Morgan",BusinessFixture.Phrase,"Fictional clean LIVE start");
}
