using System.Text.Json;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Ownership;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class OwnerInterestRecoveryTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void InvalidOwnershipCannotBecomeAVerifiedArchiveSource(bool changeShares)
    {
        using var f=new BusinessFixture();new OwnerInterests(f.Store).Confirm(f.Owner,OwnerInterestConfirmationTests.Draft(f,0,new(2027,1,1),.6m));
        f.Files.Execute(changeShares?"DROP TRIGGER OwnerInterestShares_NoUpdate; UPDATE OwnerInterestShares SET FractionText='0.1';":"DROP TRIGGER OwnerInterestVersions_NoUpdate; UPDATE OwnerInterestVersions SET SnapshotJson=replace(SnapshotJson,'Fictional','Changed');");
        Assert.Throws<InvalidDataException>(()=>{using var captured=new ArchiveSources(f.Store).Capture(f.Owner);});Assert.Equal(2L,TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));
    }
    [Fact]
    public void ARehashedConfirmationCannotClaimADifferentManagedEvidenceHash()
    {
        using var f=new BusinessFixture();var evidence=new ManagedAttachments(f.Store).Import(f.Owner,ManagedAttachmentTests.Pdf(f),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());
        var value=new OwnerInterests(f.Store).Confirm(f.Owner,OwnerInterestConfirmationTests.Draft(f,0,new(2027,1,1),.6m) with{EvidenceVersionId=evidence.Version.Id});
        string json=JsonSerializer.Serialize(value with{EvidenceSnapshotHash=new string('A',64)});f.Files.Execute("DROP TRIGGER OwnerInterestVersions_NoUpdate;");
        f.Store.Transaction(db=>db.Execute("UPDATE OwnerInterestVersions SET SnapshotJson=$json,SnapshotHash=$hash;",("$json",json),("$hash",OwnerInterestCodec.Hash(json))));
        Assert.Throws<InvalidDataException>(()=>new OwnerInterests(f.Store).History(f.Owner));
    }
    [Fact]
    public async Task EncryptedRecoveryPreservesCapturedInterestsAndEvidenceThroughANewWritableInstance()
    {
        using var ledger=new LedgerFixture();var f=ledger.Business;using var target=new TestStore();ledger.Post(ledger.Draft(FinancialKind.Receipt,12345));
        var evidence=new ManagedAttachments(f.Store).Import(f.Owner,ManagedAttachmentTests.Pdf(f),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());var interests=new OwnerInterests(f.Store);
        var first=interests.Confirm(f.Owner,OwnerInterestConfirmationTests.Draft(f,0,new(2027,1,1),.6m) with{EvidenceVersionId=evidence.Version.Id});
        string archivePath=Path.Combine(f.Files.Folder,"fictional-ownership.age");var key=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);await new FullArchives(f.Store).CreateAsync(f.Owner,archivePath,key.Recipient);
        interests.Confirm(f.Owner,OwnerInterestConfirmationTests.Draft(f,1,new(2027,1,1),.7m) with{SupersedesId=first.Id});
        using var review=await new ArchiveRestores().StageAsync(archivePath,f.Files.Folder,key.ProtectedIdentity,BusinessFixture.Phrase);
        var prepared=await new RecoveredStores().PrepareAsync(review,target.Folder,new(f.Owner.OwnerId,"Fictional ownership recovery review",true),"fictional orchard river lantern",Guid.NewGuid().ToString());
        var restored=new BusinessStore(prepared.DatabasePath,"Prepared");var owner=new OwnerAccess(restored).SignIn(f.Owner.OwnerId,"fictional orchard river lantern");var retained=Assert.Single(new OwnerInterests(restored).History(owner));
        Assert.NotEqual(f.Store.InstanceId,restored.InstanceId);Assert.Equal(first.Id,retained.Id);Assert.Equal(first.Shares,retained.Shares);Assert.Equal(evidence.SnapshotHash,retained.EvidenceSnapshotHash);Assert.Equal(2,interests.History(f.Owner).Count);
        using var bytes=new ManagedAttachments(restored).OpenRead(owner,evidence.Version.Id);Assert.Equal(evidence.Version.ByteLength,bytes.Length);Assert.Equal(12345L,TestStore.Scalar(restored.DatabasePath,"SELECT SUM(DebitCents) FROM JournalLines;"));
    }
}
