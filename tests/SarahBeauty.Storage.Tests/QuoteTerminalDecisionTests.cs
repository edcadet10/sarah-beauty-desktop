using System.Security.Cryptography;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class QuoteTerminalDecisionTests
{
    [Theory]
    [InlineData(DocumentDecisionAction.DeclineQuote,"Declined")][InlineData(DocumentDecisionAction.WithdrawQuote,"Withdrawn")][InlineData(DocumentDecisionAction.VoidQuote,"Void")]
    public void ExactIssuedVersionChangesOnceAndKeepsItsOriginalPdfAndMoney(DocumentDecisionAction action,string result)
    {
        using var f=new BusinessFixture();var version=Issue(f);string pdf=new DocumentArtifacts(f.Store).Render(f.Owner,version.Id);byte[] bytes=File.ReadAllBytes(pdf);long watermark=Watermark(f);var book=new DocumentDecisions(f.Store);var draft=Draft(version,action);string request=Guid.NewGuid().ToString();
        var value=book.RecordQuote(f.Owner,draft,BusinessFixture.Phrase,request);Assert.Equal(result,value.ResultStatus);Assert.Equal(value,book.RecordQuote(f.Owner,draft,BusinessFixture.Phrase,request));Assert.Equal(watermark+1,Watermark(f));
        Assert.Equal(value,Assert.Single(new DocumentDecisions(new BusinessStore(f.Store.DatabasePath)).History(f.Owner,version.Id)));var retained=Assert.Single(new QuoteBook(f.Store).Versions(f.Owner));Assert.Equal(result,retained.Status);Assert.Equal(version.Hash,retained.Hash);Assert.Equal(bytes,File.ReadAllBytes(new DocumentArtifacts(f.Store).VerifiedPath(f.Owner,version.Id)));
        Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM JournalLines;"));Assert.Equal(1L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM Audit WHERE Action='Record issued quote decision';"));
        Assert.Throws<ArgumentException>(()=>new QuoteBook(f.Store).Accept(f.Owner,version.Id,version.Snapshot.IssueDate,"Fictional invalid later acceptance"));
        Assert.Throws<ArgumentException>(()=>book.RecordQuote(f.Owner,draft with{Reason="Fictional different decision"},BusinessFixture.Phrase,request));
    }
    [Fact]
    public void AcceptedAgreementAndItsBookingBindingRemainWhenAProposalIsWithdrawn()
    {
        using var f=new BusinessFixture();var quotes=new QuoteBook(f.Store);var first=Issue(f);quotes.Accept(f.Owner,first.Id,first.Snapshot.IssueDate,"Fictional accepted original agreement");
        var booking=new BookingBook(f.Store).List(f.Owner).Single();var draft=quotes.SaveDraft(f.Owner,quotes.FromBooking(f.Owner,booking,first.DocumentId) with{Terms="Fictional proposed amendment"});var proposed=quotes.Issue(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        var book=new DocumentDecisions(f.Store);Assert.Throws<ArgumentException>(()=>book.RecordQuote(f.Owner,Draft(first,DocumentDecisionAction.VoidQuote),BusinessFixture.Phrase,Guid.NewGuid().ToString()));
        book.RecordQuote(f.Owner,Draft(proposed,DocumentDecisionAction.WithdrawQuote),BusinessFixture.Phrase,Guid.NewGuid().ToString());Assert.Equal("Accepted",quotes.Versions(f.Owner).Single(v=>v.Id==first.Id).Status);Assert.Equal(first.Id,new BookingBook(f.Store).List(f.Owner).Single().AcceptedQuoteVersionId);Assert.Equal("Awaiting deposit",new BookingBook(f.Store).List(f.Owner).Single().Status);
    }
    [Fact]
    public void StaleHashEarlierDateWrongActionAndForeignAccessCannotCreateADecision()
    {
        using var f=new BusinessFixture();using var foreign=new BusinessFixture();var version=Issue(f);var draft=Draft(version,DocumentDecisionAction.DeclineQuote);long watermark=Watermark(f);var book=new DocumentDecisions(f.Store);
        foreach(var invalid in new[]{draft with{DocumentSnapshotHash=new string('0',64)},draft with{DecisionDate=version.Snapshot.IssueDate.AddDays(-1)},draft with{Action=DocumentDecisionAction.VoidInvoice}})
            Assert.Throws<ArgumentException>(()=>book.RecordQuote(f.Owner,invalid,BusinessFixture.Phrase,Guid.NewGuid().ToString()));
        Assert.Throws<UnauthorizedAccessException>(()=>book.RecordQuote(foreign.Owner,draft,BusinessFixture.Phrase,Guid.NewGuid().ToString()));Assert.Empty(book.History(f.Owner));Assert.Equal(watermark,Watermark(f));Assert.Equal("Issued",Assert.Single(new QuoteBook(f.Store).Versions(f.Owner)).Status);
    }
    [Theory]
    [InlineData("cancel")][InlineData("lock")][InlineData("storage")]
    public void FailureAfterDecisionAndStatusWritesRollsBackEverything(string defect)
    {
        using var f=new BusinessFixture();var version=Issue(f);long watermark=Watermark(f);using var cancellation=new CancellationTokenSource();var book=new DocumentDecisions(f.Store){BeforeDecisionCommit=()=>
        {
            if(defect=="cancel")cancellation.Cancel();else if(defect=="lock")f.Owner.Lock();else throw new IOException("Fictional interrupted quote decision");
        }};
        var error=Record.Exception(()=>book.RecordQuote(f.Owner,Draft(version,DocumentDecisionAction.VoidQuote),BusinessFixture.Phrase,Guid.NewGuid().ToString(),cancellation.Token));Assert.True(defect switch{"cancel"=>error is OperationCanceledException,"lock"=>error is UnauthorizedAccessException,_=>error is IOException});
        Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM DocumentDecisions;"));Assert.Equal("Issued",TestStore.Scalar(f.Store.DatabasePath,"SELECT Status FROM DocumentVersions;"));Assert.Equal(watermark,Watermark(f));Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM Audit WHERE Action='Record issued quote decision';"));
    }
    [Fact]
    public void ManagedEvidenceIsVerifiedBeforeDecisionAndItsOriginalVersionIsPinned()
    {
        using var f=new BusinessFixture();var version=Issue(f);var files=new ManagedAttachments(f.Store);var file=files.Import(f.Owner,ManagedAttachmentTests.Pdf(f),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());var draft=Draft(version,DocumentDecisionAction.DeclineQuote) with{Evidence=ManagedEvidenceFile.From(file)};var book=new DocumentDecisions(f.Store);
        string path=Path.Combine(f.Files.Folder,file.Version.RelativePath);byte[] original=File.ReadAllBytes(path);File.AppendAllText(path,"Fictional changed source");long watermark=Watermark(f);
        Assert.Throws<InvalidDataException>(()=>book.RecordQuote(f.Owner,draft,BusinessFixture.Phrase,Guid.NewGuid().ToString()));Assert.Empty(book.History(f.Owner));Assert.Equal(watermark,Watermark(f));File.WriteAllBytes(path,original);
        var value=book.RecordQuote(f.Owner,draft,BusinessFixture.Phrase,Guid.NewGuid().ToString());Assert.Equal(file.SnapshotHash,value.Evidence!.MetadataHash);Assert.Equal(file.Version.Sha256,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        files.Import(f.Owner,ManagedAttachmentTests.Pdf(f,"Fictional later scan.pdf"),ManagedAttachmentTests.Draft with{AttachmentId=file.Version.AttachmentId,ExpectedVersion=1},Guid.NewGuid().ToString());Assert.Equal(file.Version.Id,Assert.Single(book.History(f.Owner)).Evidence!.VersionId);
    }
    [Fact]
    public void CompetingDecisionBeforeWriterReservationWinsWithoutASecondStatusOrAudit()
    {
        using var f=new BusinessFixture();var version=Issue(f);long watermark=Watermark(f);var competitor=new DocumentDecisions(new BusinessStore(f.Store.DatabasePath));DocumentDecision? winner=null;int reservations=0;
        f.Store.BeforeTransactionReservation=()=>
        {
            if(++reservations!=2)return;f.Store.BeforeTransactionReservation=null;
            winner=competitor.RecordQuote(f.Owner,Draft(version,DocumentDecisionAction.WithdrawQuote),BusinessFixture.Phrase,Guid.NewGuid().ToString());
        };
        Assert.Throws<ArgumentException>(()=>new DocumentDecisions(f.Store).RecordQuote(f.Owner,Draft(version,DocumentDecisionAction.DeclineQuote),BusinessFixture.Phrase,Guid.NewGuid().ToString()));
        Assert.Equal(winner,Assert.Single(competitor.History(f.Owner)));Assert.Equal("Withdrawn",TestStore.Scalar(f.Store.DatabasePath,"SELECT Status FROM DocumentVersions;"));Assert.Equal(watermark+1,Watermark(f));Assert.Equal(1L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM Audit WHERE Action='Record issued quote decision';"));
    }
    internal static DocumentVersion Issue(BusinessFixture f)
    {
        var quotes=new QuoteBook(f.Store);var draft=quotes.SaveDraft(f.Owner,quotes.FromBooking(f.Owner,QuoteTests.Booking(f)));return quotes.Issue(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }
    internal static DocumentDecisionDraft Draft(DocumentVersion version,DocumentDecisionAction action)=>new(version.Id,version.Hash,"Issued",action,version.Snapshot.IssueDate,"Fictional reviewed client decision");
    private static long Watermark(BusinessFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));
}
