using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Setup;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class ModeRecoveryTests
{
    private const string RecoveryPhrase="fictional meadow orchard lantern recovery";
    [Theory]
    [InlineData(BusinessMode.Test)][InlineData(BusinessMode.Live)]
    public async Task ExplicitModePreparationPreservesEncryptedBusinessFactsAndRejectsCrossModeSelection(BusinessMode mode)
    {
        using var f=new Fixture(mode);using var target=new TestStore();var cipher=new RecoveryCipher();var key=await cipher.CreateAsync(BusinessFixture.Phrase);string archivePath=Path.Combine(f.Files.Folder,"fictional-mode.age");var archive=await new FullArchives(f.Store).CreateAsync(f.Owner,archivePath,key.Recipient);long watermark=archive.Manifest.Watermark;
        using var review=await new ArchiveRestores().StageAsync(archivePath,f.Files.Folder,key.ProtectedIdentity,BusinessFixture.Phrase,archive);var decision=new RestoreCopyDecision(f.Owner.OwnerId,"Fictional mode-specific recovery",true);var wrongMode=mode==BusinessMode.Live?BusinessMode.Test:BusinessMode.Live;
        await Assert.ThrowsAsync<ArgumentException>(()=>new RecoveredStores(wrongMode).PrepareAsync(review,target.Folder,decision,RecoveryPhrase,Guid.NewGuid().ToString()));Assert.False(Directory.Exists(Path.Combine(target.Folder,"RestoredStores")));
        string request=Guid.NewGuid().ToString();var prepared=await new RecoveredStores(mode).PrepareAsync(review,target.Folder,decision,RecoveryPhrase,request);Assert.Equal(BusinessModes.StoredName(mode),prepared.Receipt.Mode);Assert.Equal(System.Text.Json.JsonSerializer.Serialize(archive.Manifest),System.Text.Json.JsonSerializer.Serialize(prepared.Receipt.SourceManifest));Assert.Equal(f.Store.StoreId,prepared.Receipt.StoreId);Assert.NotEqual(f.Store.InstanceId,prepared.Receipt.InstanceId);
        var store=new BusinessStore(prepared.DatabasePath,mode,"Prepared");var owner=new OwnerAccess(store).SignIn(f.Owner.OwnerId,RecoveryPhrase);Assert.Equal(mode,owner.Mode);Assert.Throws<UnauthorizedAccessException>(()=>store.BusinessName(f.Owner));Assert.Throws<UnauthorizedAccessException>(()=>new OwnerAccess(store).SignIn(f.Owner.OwnerId,BusinessFixture.Phrase));Assert.Equal(f.Profile,new BusinessProfiles(store).Current(owner));
        Assert.Equal(2300L,TestStore.Scalar(store.DatabasePath,"SELECT SUM(DebitCents) FROM JournalLines;"));Assert.Equal(2300L,TestStore.Scalar(store.DatabasePath,"SELECT SUM(CreditCents) FROM JournalLines;"));Assert.Equal(watermark+1,TestStore.Scalar(store.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Equal(watermark,TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));
        foreach(var file in review.Manifest.Files.Where(x=>x.Kind!="Database"))Assert.Equal(File.ReadAllBytes(review.FilePath(file.RelativePath)),File.ReadAllBytes(Path.Combine(prepared.Folder,file.RelativePath)));
        store.Read(db=>{ReadyStoreChecks.Verify(db,prepared.Folder,prepared.Receipt,CancellationToken.None,mode);Assert.Throws<InvalidDataException>(()=>ReadyStoreChecks.Verify(db,prepared.Folder,prepared.Receipt with{Mode=BusinessModes.StoredName(wrongMode)},CancellationToken.None,mode));return true;});
        using var cancellation=new CancellationTokenSource();string cancelled=Guid.NewGuid().ToString();var interrupted=new RecoveredStores(mode){FailurePoint=point=>{if(point=="Recovered access prepared")cancellation.Cancel();}};await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>interrupted.PrepareAsync(review,target.Folder,decision,RecoveryPhrase,cancelled,cancellation.Token));Assert.False(Directory.Exists(Path.Combine(target.Folder,"RestoredStores",Guid.Parse(cancelled).ToString("N"))));Assert.Single(Directory.GetDirectories(Path.Combine(target.Folder,"RestoredStores")));
    }
    [Fact]
    public void InvalidModeIsRejectedBeforeAnyRecoveryWork()=>Assert.Throws<ArgumentOutOfRangeException>(()=>new RecoveredStores((BusinessMode)99));

    private sealed class Fixture:IDisposable
    {
        internal TestStore Files {get;}=new();
        internal BusinessStore Store {get;}
        internal OwnerSession Owner {get;}
        internal BusinessProfileVersion Profile {get;}
        internal Fixture(BusinessMode mode)
        {
            var profile=LiveSetupPolicyTests.ReviewedProfile() with{DisplayName="Fictional mode recovery",BooksStartDate=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2)};
            if(mode==BusinessMode.Live){var created=new LiveBusinesses(Path.Combine(Files.Folder,"Live")).Create(profile,"Alex",BusinessFixture.Phrase,"Morgan",BusinessFixture.Phrase,"Fictional clean recovery fixture");Store=created.Store;Owner=created.Owner;}
            else{Store=new(Files.DatabasePath);Owner=new OwnerAccess(Store).Setup(profile.DisplayName,"Alex",BusinessFixture.Phrase,"Morgan",BusinessFixture.Phrase);new ChartOfAccounts(Store).Initialize(Owner);new BusinessProfiles(Store).Confirm(Owner,new(0,profile,"Fictional reviewed recovery fixture"),BusinessFixture.Phrase,Guid.NewGuid().ToString());}
            var quote=ModeStoreTests.IssueQuote(Store,Owner);string pdf=new DocumentArtifacts(Store).Render(Owner,quote.Id);var evidence=new ManagedAttachments(Store).Import(Owner,pdf,ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());Profile=new BusinessProfiles(Store).Confirm(Owner,new(1,profile with{Formation=profile.Formation with{Evidence=ManagedEvidenceFile.From(evidence)}},"Fictional retained setup evidence"),BusinessFixture.Phrase,Guid.NewGuid().ToString());
            var accounts=new ChartOfAccounts(Store).List(Owner).ToDictionary(a=>a.Code);string customer=new CustomerBook(Store).Search(Owner).Single().Id;var money=new FinanceBook(Store);var draft=money.SaveDraft(Owner,new(){Kind=FinancialKind.Receipt,Name="Fictional mode recovery receipt",Date=quote.Snapshot.IssueDate,AmountCents=2300,TaxResolved=true,CashAccountId=accounts["1010"].Id,CounterAccountId=accounts["4000"].Id,CustomerId=customer});money.Approve(Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        }
        public void Dispose()=>Files.Dispose();
    }
}
