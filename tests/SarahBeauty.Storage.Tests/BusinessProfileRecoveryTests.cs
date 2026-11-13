using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Setup;

namespace SarahBeauty.Storage.Tests;

public sealed class BusinessProfileRecoveryTests
{
    [Fact]
    public async Task PrivateFactsAndPinnedEvidenceRestoreWithFreshOwnerAccessWithoutInventingSetup()
    {
        using var ledger=new LedgerFixture();var f=ledger.Business;using var target=new TestStore();ledger.Post(ledger.Draft(FinancialKind.Receipt,12345));var file=new ManagedAttachments(f.Store).Import(f.Owner,ManagedAttachmentTests.Pdf(f),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());var draft=BusinessProfileConfirmTests.Draft() with{Profile=BusinessProfileConfirmTests.Draft().Profile with{Ein="00-0000000",EinReview=new(BusinessSetupState.Confirmed,"Fictional private EIN review"),Formation=new(BusinessSetupState.InProgress,"Fictional formation review pending",ManagedEvidenceFile.From(file))}};var saved=new BusinessProfiles(f.Store).Confirm(f.Owner,draft,BusinessFixture.Phrase,Guid.NewGuid().ToString());var material=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);string path=Path.Combine(f.Files.Folder,"fictional-private-profile.age");var archive=await new FullArchives(f.Store).CreateAsync(f.Owner,path,material.Recipient);f.Owner.Lock();
        using var review=await new ArchiveRestores().StageAsync(path,target.Folder,material.ProtectedIdentity,BusinessFixture.Phrase,archive);const string phrase="fictional orchard meadow lantern";string request=Guid.NewGuid().ToString();await new RecoveredStores().PrepareAsync(review,target.Folder,new(f.Owner.OwnerId,"Fictional private-profile recovery review",true),phrase,request);var active=await new ActiveTestStores(target.Folder).ActivateAsync(request,f.Owner.OwnerId,phrase);
        var retained=new BusinessProfiles(active.Store).Current(active.Owner);Assert.Equal(saved,retained);Assert.Equal("00-0000000",retained!.Profile.Ein);Assert.Equal(BusinessSetupState.InProgress,retained.Profile.Formation.State);Assert.Equal(BusinessSetupState.Unconfirmed,retained.Profile.Tax.State);Assert.Null(retained.Profile.BooksStartDate);Assert.Single(new BusinessProfiles(active.Store).History(active.Owner));using var evidence=new ManagedAttachments(active.Store).OpenRead(active.Owner,file.Version.Id);Assert.Equal(file.Version.ByteLength,evidence.Length);
        Assert.Equal("TEST",TestStore.Scalar(active.Store.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='StoreMode';"));Assert.Equal(12345L,TestStore.Scalar(active.Store.DatabasePath,"SELECT SUM(DebitCents) FROM JournalLines;"));Assert.Equal(12345L,TestStore.Scalar(active.Store.DatabasePath,"SELECT SUM(CreditCents) FROM JournalLines;"));Assert.NotEqual(f.Store.InstanceId,active.Store.InstanceId);Assert.False(f.Owner.IsActive);
    }
}
