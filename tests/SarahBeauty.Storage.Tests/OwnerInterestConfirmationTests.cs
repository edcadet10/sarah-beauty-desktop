using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Ownership;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class OwnerInterestConfirmationTests
{
    [Fact]
    public void ConfirmedCorrectionsSurviveReopenWithoutAllocatingPostedMoney()
    {
        using var ledger=new LedgerFixture();var f=ledger.Business;ledger.Post(ledger.Draft(FinancialKind.Receipt,12345));long before=Watermark(f);var interests=new OwnerInterests(f.Store);
        var first=interests.Confirm(f.Owner,Draft(f,0,new(2027,1,1),.6m));var later=interests.Confirm(f.Owner,Draft(f,1,new(2027,4,1),.5m));
        var corrected=interests.Confirm(f.Owner,Draft(f,2,new(2027,1,1),.7m) with{SupersedesId=first.Id,Reason="Fictional corrected agreement"});
        var reopened=new BusinessStore(f.Store.DatabasePath);var owner=new OwnerAccess(reopened).SignIn(f.Owner.OwnerId,BusinessFixture.Phrase);interests=new(reopened);
        Assert.Equal(new[]{first.Id,later.Id,corrected.Id},interests.History(owner).Select(v=>v.Id));Assert.Equal(corrected.Id,interests.At(owner,new(2027,3,31))!.Confirmation.Id);Assert.Equal(later.Id,interests.At(owner,new(2027,4,1))!.Confirmation.Id);
        Assert.Equal(before+3,Watermark(f));Assert.Equal(3L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM Audit WHERE Action='Confirmed dated ownership percentages';"));
        Assert.Equal(1L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM Movements;"));Assert.Equal(12345L,TestStore.Scalar(f.Store.DatabasePath,"SELECT SUM(DebitCents) FROM JournalLines;"));Assert.Equal(12345L,TestStore.Scalar(f.Store.DatabasePath,"SELECT SUM(CreditCents) FROM JournalLines;"));
    }
    [Fact]
    public void MissingOrForeignOwnersAndInvalidCorrectionsCannotCreateHistory()
    {
        using var f=new BusinessFixture();using var foreign=new BusinessFixture();var interests=new OwnerInterests(f.Store);var draft=Draft(f,0,new(2027,1,1),.6m);
        foreach(var invalid in new[]{draft with{Shares=[new(f.Owner.OwnerId,1m)]},draft with{Shares=[new(f.Owner.OwnerId,.6m),new(foreign.Owner.OwnerId,.4m)]},draft with{SupersedesId=Guid.NewGuid().ToString()}})
            Assert.Throws<ArgumentException>(()=>interests.Confirm(f.Owner,invalid));
        Assert.Empty(interests.History(f.Owner));Assert.Equal(1L,Watermark(f));var first=interests.Confirm(f.Owner,draft);
        Assert.Throws<InvalidOperationException>(()=>interests.Confirm(f.Owner,draft));Assert.Throws<ArgumentException>(()=>interests.Confirm(f.Owner,draft with{ExpectedRevision=1}));
        Assert.Throws<ArgumentException>(()=>interests.Confirm(f.Owner,draft with{ExpectedRevision=1,EffectiveFrom=new(2027,2,1),SupersedesId=first.Id}));
        var correction=interests.Confirm(f.Owner,draft with{ExpectedRevision=1,SupersedesId=first.Id});
        Assert.Throws<ArgumentException>(()=>interests.Confirm(f.Owner,draft with{ExpectedRevision=2,SupersedesId=first.Id}));Assert.Equal(2,interests.History(f.Owner).Count);
    }
    [Fact]
    public void ACompetingWriterWinsOnceAndTheStaleDraftCannotOverwriteIt()
    {
        using var f=new BusinessFixture();var draft=Draft(f,0,new(2027,1,1),.6m);var otherStore=new BusinessStore(f.Store.DatabasePath);var competitor=new OwnerInterests(otherStore);OwnerInterestVersion? saved=null;
        f.Store.BeforeTransactionReservation=()=>{f.Store.BeforeTransactionReservation=null;saved=competitor.Confirm(f.Owner,draft with{Shares=Draft(f,0,draft.EffectiveFrom,.7m).Shares});};
        Assert.Throws<InvalidOperationException>(()=>new OwnerInterests(f.Store).Confirm(f.Owner,draft));Assert.Equal(saved!.Id,Assert.Single(competitor.History(f.Owner)).Id);Assert.Equal(2L,Watermark(f));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void CancellationOrLockAfterWritingRollsBackTheEntireConfirmation(bool lockOwner)
    {
        using var f=new BusinessFixture();using var cancellation=new CancellationTokenSource();var interests=new OwnerInterests(f.Store){BeforeConfirmationCommit=()=>{if(lockOwner)f.Owner.Lock();else cancellation.Cancel();}};
        var draft=Draft(f,0,new(2027,1,1),.6m);
        if(lockOwner)Assert.Throws<UnauthorizedAccessException>(()=>interests.Confirm(f.Owner,draft,cancellation.Token));else Assert.Throws<OperationCanceledException>(()=>interests.Confirm(f.Owner,draft,cancellation.Token));
        Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM OwnerInterestVersions;"));Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM OwnerInterestShares;"));Assert.Equal(1L,Watermark(f));
        Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM Audit WHERE Action='Confirmed dated ownership percentages';"));
    }
    [Fact]
    public void InactiveLoginRetainsOwnershipAndExactZeroCanBeConfirmed()
    {
        using var f=new BusinessFixture();string other=new OwnerInterests(f.Store).Owners(f.Owner).Single(o=>o.Id!=f.Owner.OwnerId).Id;
        f.Store.Transaction(db=>db.Execute("UPDATE Owners SET Active=0 WHERE Id=$id;",("$id",other)));var value=new OwnerInterests(f.Store).Confirm(f.Owner,Draft(f,0,new(2027,1,1),0m));
        Assert.Equal(1m,value.Shares.Single(s=>s.OwnerId==other).Fraction);Assert.Equal(0m,value.Shares.Single(s=>s.OwnerId==f.Owner.OwnerId).Fraction);
    }
    [Fact]
    public void RealManagedEvidencePinsItsOriginalVersionAndMissingFilesCannotConfirm()
    {
        using var f=new BusinessFixture();using var foreign=new BusinessFixture();var files=new ManagedAttachments(f.Store);var imported=files.Import(f.Owner,ManagedAttachmentTests.Pdf(f),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());
        var interests=new OwnerInterests(f.Store);var draft=Draft(f,0,new(2027,1,1),.6m) with{EvidenceVersionId=imported.Version.Id};var first=interests.Confirm(f.Owner,draft);
        var replacement=files.Import(f.Owner,ManagedAttachmentTests.Pdf(f,"Fictional new agreement.pdf"),ManagedAttachmentTests.Draft with{AttachmentId=imported.Version.AttachmentId,ExpectedVersion=1},Guid.NewGuid().ToString());
        Assert.Equal(imported.Version.Id,Assert.Single(interests.History(f.Owner)).EvidenceVersionId);Assert.Equal(imported.SnapshotHash,first.EvidenceSnapshotHash);Assert.NotEqual(replacement.Version.Id,first.EvidenceVersionId);
        long before=Watermark(f);File.Delete(Path.Combine(f.Files.Folder,imported.Version.RelativePath));Assert.Throws<InvalidDataException>(()=>interests.Confirm(f.Owner,draft with{ExpectedRevision=1,EffectiveFrom=new(2027,2,1)}));Assert.Equal(before,Watermark(f));Assert.Single(interests.History(f.Owner));
        var foreignFile=new ManagedAttachments(foreign.Store).Import(foreign.Owner,ManagedAttachmentTests.Pdf(foreign),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());
        Assert.Throws<ArgumentException>(()=>interests.Confirm(f.Owner,draft with{ExpectedRevision=1,EffectiveFrom=new(2027,2,1),EvidenceVersionId=foreignFile.Version.Id}));
    }
    internal static OwnerInterestDraft Draft(BusinessFixture f,long revision,DateOnly effective,decimal first)=>new(revision,effective,new OwnerInterests(f.Store).Owners(f.Owner).Select(o=>new OwnerInterestInput(o.Id,o.Id==f.Owner.OwnerId?first:1-first)).ToArray(),"Fictional owner-reviewed agreement");
    private static long Watermark(BusinessFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));
}
