using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Setup;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class LiveBusinessCreationTests
{
    [Fact]
    public void CleanCreationPreservesUnknownFactsAndNeverCopiesTheTestBusiness()
    {
        using var test=new LedgerFixture();test.Post(test.Draft(FinancialKind.Receipt,2500));var originalBusiness=test.Business;string original=originalBusiness.Files.Hash();
        string root=Path.Combine(originalBusiness.Files.Folder,"SeparateLive");var result=Create(new(root));var store=result.Store;var owner=result.Owner;
        Assert.Equal(BusinessMode.Live,store.Mode);Assert.Equal(BusinessMode.Live,owner.Mode);Assert.NotEqual(originalBusiness.Store.StoreId,store.StoreId);Assert.NotEqual(originalBusiness.Store.InstanceId,store.InstanceId);Assert.NotEqual(originalBusiness.Owner.BusinessId,owner.BusinessId);Assert.Equal(original,originalBusiness.Files.Hash());
        var saved=new BusinessProfiles(store).Current(owner)!;Assert.Equal(Profile(),saved.Profile);Assert.Equal("LIVE",saved.Mode);Assert.Equal(1,saved.Revision);Assert.Equal(5,LiveSetupPolicy.PendingOperationalReviews(saved.Profile).Count);
        Assert.Equal(2,new OwnerAccess(store).Owners().Count);Assert.Equal(3L,TestStore.Scalar(store.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Equal(3L,TestStore.Scalar(store.DatabasePath,"SELECT count(*) FROM Audit;"));
        foreach(string table in new[]{"Customers","Movements","JournalLines","DocumentVersions","OwnerInterestVersions","AttachmentVersions"})Assert.Equal(0L,TestStore.Scalar(store.DatabasePath,$"SELECT count(*) FROM {table};"));
        Assert.NotEmpty(new ChartOfAccounts(store).List(owner));Assert.All(new ChartOfAccounts(store).List(owner),a=>Assert.False(a.OpeningConfirmed));
        Assert.Throws<UnauthorizedAccessException>(()=>store.BusinessName(originalBusiness.Owner));Assert.Throws<UnauthorizedAccessException>(()=>originalBusiness.Store.BusinessName(owner));Assert.Empty(Directory.GetDirectories(root,".pending-live-*"));
        owner.Lock();var reopened=new BusinessStore(store.DatabasePath,BusinessMode.Live);var signedIn=new OwnerAccess(reopened).SignIn(owner.OwnerId,BusinessFixture.Phrase);Assert.Equal(BusinessMode.Live,signedIn.Mode);Assert.Equal(Profile(),new BusinessProfiles(reopened).Current(signedIn)!.Profile);
    }
    [Theory]
    [InlineData("scope")][InlineData("currency")][InlineData("zone")][InlineData("start")][InlineData("reason")][InlineData("owners")][InlineData("phrase")]
    public void InvalidCreationInputsDoNotCreateFolders(string invalid)
    {
        using var f=new TestStore();string root=Path.Combine(f.Folder,"Live");var profile=Profile();string reason="Fictional clean start",name="Morgan",phrase=BusinessFixture.Phrase;
        switch(invalid){case "scope":profile=profile with{NewBusinessOnlyConfirmed=false};break;case "currency":profile=profile with{Currency=null};break;case "zone":profile=profile with{TimeZoneId=null};break;case "start":profile=profile with{BooksStartDate=null};break;case "reason":reason="";break;case "owners":name="Alex";break;case "phrase":phrase="short";break;}
        Assert.Throws<ArgumentException>(()=>new LiveBusinesses(root).Create(profile,"Alex",BusinessFixture.Phrase,name,phrase,reason));Assert.False(Directory.Exists(root));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void ExistingDestinationIsNeverOverwrittenEvenWhenEmpty(bool isFile)
    {
        using var f=new TestStore();string root=Path.Combine(f.Folder,"Live"),destination=Path.Combine(root,"Primary");Directory.CreateDirectory(root);if(isFile)File.WriteAllText(destination,"Fictional sentinel");else Directory.CreateDirectory(destination);
        Assert.Throws<IOException>(()=>Create(new(root)));if(isFile)Assert.Equal("Fictional sentinel",File.ReadAllText(destination));else Assert.Empty(Directory.EnumerateFileSystemEntries(destination));Assert.Empty(Directory.GetDirectories(root,".pending-live-*"));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void FailureOrCancellationBeforePublicationPreservesOnlyAnUnselectedStage(bool cancel)
    {
        using var f=new TestStore();string root=Path.Combine(f.Folder,"Live");using var cancellation=new CancellationTokenSource();var businesses=new LiveBusinesses(root){BeforePublish=()=>{if(cancel)cancellation.Cancel();else throw new IOException("Fictional interrupted setup");}};
        if(cancel)Assert.Throws<OperationCanceledException>(()=>Create(businesses,cancellation.Token));else Assert.Throws<IOException>(()=>Create(businesses,cancellation.Token));
        Assert.False(Directory.Exists(Path.Combine(root,"Primary")));string pending=Assert.Single(Directory.GetDirectories(root,".pending-live-*"));Assert.Equal(1L,TestStore.Scalar(Path.Combine(pending,"sarahbeauty.db"),"SELECT count(*) FROM BusinessProfileVersions;"));
    }
    [Fact]
    public void CompetingCreationCanPublishOnlyOneBusiness()
    {
        using var f=new TestStore();string root=Path.Combine(f.Folder,"Live");CreatedLiveBusiness? winner=null;var loser=new LiveBusinesses(root){BeforePublish=()=>winner=Create(new(root))};Assert.Throws<IOException>(()=>Create(loser));Assert.NotNull(winner);Assert.Equal(winner.Store.StoreId,new BusinessStore(Path.Combine(root,"Primary","sarahbeauty.db"),BusinessMode.Live).StoreId);Assert.Single(Directory.GetDirectories(root,".pending-live-*"));Assert.Equal(1L,TestStore.Scalar(winner.Store.DatabasePath,"SELECT count(*) FROM Business;"));
    }
    [Fact]
    public void DestinationCreatedDuringSetupIsPreserved()
    {
        using var f=new TestStore();string root=Path.Combine(f.Folder,"Live"),target=Path.Combine(root,"Primary");var businesses=new LiveBusinesses(root){BeforePublish=()=>{Directory.CreateDirectory(target);File.WriteAllText(Path.Combine(target,"sentinel.txt"),"Fictional competing destination");}};
        Assert.Throws<IOException>(()=>Create(businesses));Assert.Equal("Fictional competing destination",File.ReadAllText(Path.Combine(target,"sentinel.txt")));Assert.False(File.Exists(Path.Combine(target,"sarahbeauty.db")));Assert.Single(Directory.GetDirectories(root,".pending-live-*"));
    }
    [Fact]
    public void PreCancelledCreationDoesNotCreateFolders()
    {
        using var f=new TestStore();using var cancellation=new CancellationTokenSource();cancellation.Cancel();string root=Path.Combine(f.Folder,"Live");Assert.Throws<OperationCanceledException>(()=>Create(new(root),cancellation.Token));Assert.False(Directory.Exists(root));
    }
    [Fact]
    public void ForeignManagedEvidenceCannotBeCopiedIntoTheCleanBusiness()
    {
        using var f=new TestStore();string root=Path.Combine(f.Folder,"Live");var file=new SarahBeauty.Business.Attachments.ManagedEvidenceFile(Guid.NewGuid().ToString("N"),new string('A',64),"Fictional evidence",1,"General","fictional.pdf","application/pdf",100,new string('B',64));
        var profile=Profile() with{Formation=new(Evidence:file)};Assert.Throws<ArgumentException>(()=>new LiveBusinesses(root).Create(profile,"Alex",BusinessFixture.Phrase,"Morgan",BusinessFixture.Phrase,"Fictional clean start"));Assert.False(Directory.Exists(root));
    }
    private static CreatedLiveBusiness Create(LiveBusinesses businesses,CancellationToken cancellation=default)=>businesses.Create(Profile(),"Alex",BusinessFixture.Phrase,"Morgan",BusinessFixture.Phrase,"Fictional clean LIVE business",cancellation);
    private static BusinessProfile Profile()=>new(){DisplayName="Fictional clean LIVE business",Currency="USD",TimeZoneId="UTC",BooksStartDate=new DateOnly(2026,11,12),NewBusinessOnlyConfirmed=true};
}
