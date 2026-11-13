using System.Text.Json;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Recovery;

namespace SarahBeauty.Storage.Tests;

public sealed class DocumentDecisionRecoveryTests
{
    [Theory]
    [InlineData("json")][InlineData("payload")][InlineData("status")][InlineData("mode")]
    public void InvalidDecisionOrSourceModeCannotBecomeAVerifiedArchiveSource(string defect)
    {
        using var f=new BusinessFixture();var version=QuoteTerminalDecisionTests.Issue(f);var decision=new DocumentDecisions(f.Store).RecordQuote(f.Owner,QuoteTerminalDecisionTests.Draft(version,DocumentDecisionAction.DeclineQuote),BusinessFixture.Phrase,Guid.NewGuid().ToString());
        if(defect=="status")f.Files.Execute("UPDATE DocumentVersions SET Status='Issued';");
        else
        {
            f.Files.Execute("DROP TRIGGER DocumentDecisions_NoUpdate;");
            if(defect=="json")f.Files.Execute("UPDATE DocumentDecisions SET SnapshotJson=replace(SnapshotJson,'Fictional','Changed');");
            else if(defect=="payload")f.Files.Execute("UPDATE DocumentDecisions SET PayloadHash=printf('%064d',0);");
            else
            {
                f.Files.Execute("DROP TRIGGER DocumentVersions_Immutable;");string source=JsonSerializer.Serialize(version.Snapshot with{Mode="LIVE"}),hash=DocumentDecisionCodec.Hash(source);var changed=decision with{DocumentSnapshotHash=hash};string json=JsonSerializer.Serialize(changed);
                f.Store.Transaction(db=>db.Execute("UPDATE DocumentVersions SET DataJson=$source,SnapshotHash=$sourceHash; UPDATE DocumentDecisions SET SnapshotJson=$json,SnapshotHash=$hash,PayloadHash=$payload;",("$source",source),("$sourceHash",hash),("$json",json),("$hash",DocumentDecisionCodec.Hash(json)),("$payload",DocumentDecisionCodec.Payload(DocumentDecisionCodec.Draft(changed)))));
            }
        }
        Assert.Throws<InvalidDataException>(()=>{using var snapshot=new ArchiveSources(f.Store).Capture(f.Owner);});
    }
    [Fact]
    public async Task DeclinedVersionPdfEvidenceAndPostedMoneyRestoreAndActivateWithoutReissuing()
    {
        using var ledger=new LedgerFixture();var f=ledger.Business;using var target=new TestStore();ledger.Post(ledger.Draft(FinancialKind.Receipt,12345));var version=QuoteTerminalDecisionTests.Issue(f);string originalPdf=new DocumentArtifacts(f.Store).Render(f.Owner,version.Id);byte[] pdf=File.ReadAllBytes(originalPdf);
        var file=new ManagedAttachments(f.Store).Import(f.Owner,ManagedAttachmentTests.Pdf(f),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());var decision=new DocumentDecisions(f.Store).RecordQuote(f.Owner,QuoteTerminalDecisionTests.Draft(version,DocumentDecisionAction.DeclineQuote) with{Evidence=ManagedEvidenceFile.From(file)},BusinessFixture.Phrase,Guid.NewGuid().ToString());
        var material=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);string archivePath=Path.Combine(f.Files.Folder,"fictional-decision-recovery.age");var archive=await new FullArchives(f.Store).CreateAsync(f.Owner,archivePath,material.Recipient);
        var booking=new SarahBeauty.Business.Bookings.BookingBook(f.Store).List(f.Owner).Single();var quotes=new QuoteBook(f.Store);var later=quotes.SaveDraft(f.Owner,quotes.FromBooking(f.Owner,booking,version.DocumentId));quotes.Issue(f.Owner,later.Id,later.Revision,BusinessFixture.Phrase);
        using var review=await new ArchiveRestores().StageAsync(archivePath,target.Folder,material.ProtectedIdentity,BusinessFixture.Phrase,archive);const string phrase="fictional orchard meadow lantern";string request=Guid.NewGuid().ToString();
        await new RecoveredStores().PrepareAsync(review,target.Folder,new(f.Owner.OwnerId,"Fictional full document-decision recovery review",true),phrase,request);var activated=await new ActiveTestStores(target.Folder).ActivateAsync(request,f.Owner.OwnerId,phrase);
        Assert.Equal(decision,Assert.Single(new DocumentDecisions(activated.Store).History(activated.Owner)));var restored=Assert.Single(new QuoteBook(activated.Store).Versions(activated.Owner));Assert.Equal(version.Id,restored.Id);Assert.Equal("Declined",restored.Status);Assert.Equal(version.Hash,restored.Hash);Assert.Equal(2,quotes.Versions(f.Owner).Count);
        Assert.Equal(pdf,File.ReadAllBytes(new DocumentArtifacts(activated.Store).VerifiedPath(activated.Owner,version.Id)));using var evidence=new ManagedAttachments(activated.Store).OpenRead(activated.Owner,file.Version.Id);Assert.Equal(file.Version.ByteLength,evidence.Length);
        Assert.Equal(12345L,TestStore.Scalar(activated.Store.DatabasePath,"SELECT SUM(DebitCents) FROM JournalLines;"));Assert.Equal(12345L,TestStore.Scalar(activated.Store.DatabasePath,"SELECT SUM(CreditCents) FROM JournalLines;"));Assert.NotEqual(f.Store.InstanceId,activated.Store.InstanceId);
    }
}
