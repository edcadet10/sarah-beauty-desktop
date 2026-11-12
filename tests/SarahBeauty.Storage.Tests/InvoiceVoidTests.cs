using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class InvoiceVoidTests
{
    private sealed class Clock:TimeProvider {public override DateTimeOffset GetUtcNow()=>new(2027,2,1,12,0,0,TimeSpan.Zero);}
    internal static DocumentDecisions Book(LedgerFixture f)=>new(f.Business.Store,new Clock());
    internal static DocumentDecisionDraft Draft(DocumentVersion invoice)=>new(invoice.Id,invoice.Hash,"Issued",DocumentDecisionAction.VoidInvoice,new(2027,1,20),"Fictional unused invoice entered in error");
    private static long Scalar(LedgerFixture f,string sql)=>Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,sql));

    [Fact]
    public void VoidRetainsTheIssuedPdfAndHistoryWithOneAuditAndGenerationAdvance()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,5000,.09m);var artifacts=new DocumentArtifacts(f.Business.Store);string path=artifacts.Render(f.Business.Owner,invoice.Id);byte[] pdf=File.ReadAllBytes(path);long watermark=Scalar(f,"SELECT Watermark FROM Business;");
        var book=Book(f);string request=Guid.NewGuid().ToString();var draft=Draft(invoice);var saved=book.VoidInvoice(f.Business.Owner,draft,BusinessFixture.Phrase,request);
        Assert.Equal("Void",saved.ResultStatus);Assert.Equal(saved,book.VoidInvoice(f.Business.Owner,draft,BusinessFixture.Phrase,request));Assert.Equal(saved,Assert.Single(new DocumentDecisions(new BusinessStore(f.Business.Store.DatabasePath)).History(f.Business.Owner)));
        Assert.Equal(watermark+1,Scalar(f,"SELECT Watermark FROM Business;"));Assert.Equal(1,Scalar(f,"SELECT count(*) FROM Audit WHERE Action='Void unused invoice';"));Assert.Equal(2,Scalar(f,"SELECT Generation FROM BillingState;"));Assert.Equal(1,Scalar(f,"SELECT Generation FROM ValueState WHERE Kind='Invoice';"));
        var retained=new QuoteBook(f.Business.Store).Versions(f.Business.Owner).Single(v=>v.Id==invoice.Id);Assert.Equal(invoice.Hash,retained.Hash);Assert.Equal(System.Text.Json.JsonSerializer.Serialize(invoice.Snapshot),System.Text.Json.JsonSerializer.Serialize(retained.Snapshot));Assert.Equal("Void",retained.Status);Assert.Equal(pdf,File.ReadAllBytes(artifacts.VerifiedPath(f.Business.Owner,invoice.Id)));
        Assert.Equal(5450,new PaymentApplications(f.Business.Store).Outstanding(f.Business.Owner,new(2027,1,19)).Sum(d=>d.Cents));Assert.Empty(new PaymentApplications(f.Business.Store).Outstanding(f.Business.Owner,new(2027,1,20)));Assert.Equal(0,Scalar(f,"SELECT count(*) FROM Movements;"));Assert.Equal(0,Scalar(f,"SELECT count(*) FROM JournalLines;"));
        Assert.Throws<ArgumentException>(()=>book.VoidInvoice(f.Business.Owner,draft,BusinessFixture.Phrase,Guid.NewGuid().ToString()));Assert.Throws<ArgumentException>(()=>book.VoidInvoice(f.Business.Owner,draft with{Reason="Fictional changed retry"},BusinessFixture.Phrase,request));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void PaymentHistoryBlocksVoidEvenAfterTheApplicationWasFullyReversed(bool reversed)
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,5000);var receipt=f.Post(f.Draft(FinancialKind.Receipt,5000));var apps=new PaymentApplications(f.Business.Store);
        var draft=apps.SaveDraft(f.Business.Owner,new(){Name="Fictional paid invoice",PaymentId=receipt.Id,InvoiceVersionId=invoice.Id,Cents=5000,Date=f.Date});var applied=Assert.Single(apps.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        if(reversed){var reverse=apps.SaveDraft(f.Business.Owner,new(){Name="Fictional full unapplication",ReversesId=applied.Id,Cents=5000,Date=f.Date.AddDays(1)});apps.Approve(f.Business.Owner,reverse.Id,reverse.Revision,BusinessFixture.Phrase);}
        long watermark=Scalar(f,"SELECT Watermark FROM Business;");Assert.Throws<ArgumentException>(()=>Book(f).VoidInvoice(f.Business.Owner,Draft(invoice),BusinessFixture.Phrase,Guid.NewGuid().ToString()));Assert.Equal(watermark,Scalar(f,"SELECT Watermark FROM Business;"));Assert.Empty(Book(f).History(f.Business.Owner));
    }

    [Fact]
    public void CreditNoteHistoryBlocksVoidEvenWhenTheWholeInvoiceWasReduced()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,5000);var credits=new CreditNoteBook(f.Business.Store);var draft=credits.SaveDraft(f.Business.Owner,new(){Name="Fictional full reduction",InvoiceVersionId=invoice.Id,IssueDate=f.Date.AddDays(1),Portions=invoice.Snapshot.Lines.Select(l=>new InvoicePortion(l.ChargeId,l.Quantity)).ToArray()});credits.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        long watermark=Scalar(f,"SELECT Watermark FROM Business;");Assert.Throws<ArgumentException>(()=>Book(f).VoidInvoice(f.Business.Owner,Draft(invoice),BusinessFixture.Phrase,Guid.NewGuid().ToString()));Assert.Equal(watermark,Scalar(f,"SELECT Watermark FROM Business;"));Assert.Empty(Book(f).History(f.Business.Owner));
    }

    [Theory]
    [InlineData("cancel")][InlineData("lock")][InlineData("storage")]
    public void APostWriteFailureRollsBackStatusDecisionAuditAndBothGenerations(string failure)
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,5000);long watermark=Scalar(f,"SELECT Watermark FROM Business;");using var cancel=new CancellationTokenSource();bool reached=false;var book=Book(f);book.BeforeDecisionCommit=()=>{reached=true;if(failure=="cancel")cancel.Cancel();else if(failure=="lock")f.Business.Owner.Lock();else throw new IOException("Fictional post-write failure");};
        var error=Record.Exception(()=>book.VoidInvoice(f.Business.Owner,Draft(invoice),BusinessFixture.Phrase,Guid.NewGuid().ToString(),cancel.Token));Assert.True(reached);Assert.IsType(failure=="cancel"?typeof(OperationCanceledException):failure=="lock"?typeof(UnauthorizedAccessException):typeof(IOException),error);Assert.Equal(watermark,Scalar(f,"SELECT Watermark FROM Business;"));Assert.Equal(0,Scalar(f,"SELECT count(*) FROM DocumentDecisions;"));Assert.Equal(0,Scalar(f,"SELECT count(*) FROM ValueState;"));Assert.Equal(1,Scalar(f,"SELECT Generation FROM BillingState;"));Assert.Equal("Issued",TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Status FROM DocumentVersions V JOIN Documents D ON D.Id=V.DocumentId WHERE D.Type='Invoice';"));
    }

    [Fact]
    public void SourceDateModeAndClosedPeriodAreCheckedBeforeWriting()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,5000);var book=Book(f);var draft=Draft(invoice);long watermark=Scalar(f,"SELECT Watermark FROM Business;");
        foreach(var invalid in new[]{draft with{DocumentSnapshotHash=new('A',64)},draft with{DecisionDate=f.Date.AddDays(-1)},draft with{DecisionDate=new(2027,2,2)},draft with{Action=DocumentDecisionAction.VoidQuote}})Assert.Throws<ArgumentException>(()=>book.VoidInvoice(f.Business.Owner,invalid,BusinessFixture.Phrase,Guid.NewGuid().ToString()));
        f.Business.Files.Execute("UPDATE BookSettings SET ClosedThrough='2027-01-31';");Assert.Throws<ArgumentException>(()=>book.VoidInvoice(f.Business.Owner,draft,BusinessFixture.Phrase,Guid.NewGuid().ToString()));f.Business.Files.Execute("UPDATE BookSettings SET ClosedThrough=NULL;");
        using var foreign=new BusinessFixture();Assert.Throws<UnauthorizedAccessException>(()=>book.VoidInvoice(foreign.Owner,draft,BusinessFixture.Phrase,Guid.NewGuid().ToString()));Assert.Empty(book.History(f.Business.Owner));Assert.Equal(watermark,Scalar(f,"SELECT Watermark FROM Business;"));
    }

    [Fact]
    public void ReplacementInvoiceCannotReuseTheVoidedCoverageBeforeItsEffectiveDate()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,5000);Book(f).VoidInvoice(f.Business.Owner,Draft(invoice),BusinessFixture.Phrase,Guid.NewGuid().ToString());
        var book=new InvoiceBook(f.Business.Store);string quote=Convert.ToString(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT SourceQuoteVersionId FROM InvoiceHeaders;"))!;
        var draft=book.SaveDraft(f.Business.Owner,new(){Name="Fictional replacement",QuoteVersionId=quote,PayerCustomerId=f.CustomerId,IssueDate=new(2027,1,19),Portions=invoice.Snapshot.Lines.Select(l=>new InvoicePortion(l.ChargeId,l.Quantity)).ToArray()});
        Assert.Throws<ArgumentException>(()=>book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        draft=book.SaveDraft(f.Business.Owner,draft with{IssueDate=new(2027,1,20)});var replacement=book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);Assert.NotEqual(invoice.Snapshot.Number,replacement.Snapshot.Number);Assert.Equal(5000,new PaymentApplications(f.Business.Store).Outstanding(f.Business.Owner,new(2027,1,20)).Sum(d=>d.Cents));Assert.Equal(3,Scalar(f,"SELECT Generation FROM BillingState;"));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void CompetingApplicationAndVoidCannotBothCommit(bool voidWins)
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,5000);var payment=f.Post(f.Draft(FinancialKind.Receipt,5000));var apps=new PaymentApplications(f.Business.Store);var draft=apps.SaveDraft(f.Business.Owner,new(){Name="Fictional competing application",PaymentId=payment.Id,InvoiceVersionId=invoice.Id,Cents=5000,Date=f.Date});
        var other=new BusinessStore(f.Business.Store.DatabasePath);int reservations=0;bool competed=false;f.Business.Store.BeforeTransactionReservation=()=>
        {
            if(++reservations!=2)return;f.Business.Store.BeforeTransactionReservation=null;
            if(voidWins)new DocumentDecisions(other,new Clock()).VoidInvoice(f.Business.Owner,Draft(invoice),BusinessFixture.Phrase,Guid.NewGuid().ToString());else new PaymentApplications(other).Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
            competed=true;
        };
        Assert.Throws<ArgumentException>(()=>{if(voidWins)apps.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);else Book(f).VoidInvoice(f.Business.Owner,Draft(invoice),BusinessFixture.Phrase,Guid.NewGuid().ToString());});
        Assert.True(competed);Assert.Equal(voidWins?1:0,Scalar(f,"SELECT count(*) FROM DocumentDecisions;"));Assert.Equal(voidWins?0:1,Scalar(f,"SELECT count(*) FROM PaymentApplications;"));Assert.Equal(voidWins?1:0,Scalar(f,"SELECT count(*) FROM Audit WHERE Action='Void unused invoice';"));
    }

    [Fact]
    public void UpgradeFromThePriorWriterPreservesIssuedRecordsAndItsOpenableSnapshot()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,5000);long watermark=Scalar(f,"SELECT Watermark FROM Business;");string instance=f.Business.Store.InstanceId;f.Business.Files.Execute("PRAGMA user_version=36;");
        var upgraded=new BusinessStore(f.Business.Store.DatabasePath);Assert.Equal(37,Scalar(f,"PRAGMA user_version;"));Assert.Equal(instance,upgraded.InstanceId);Assert.Equal(watermark,Scalar(f,"SELECT Watermark FROM Business;"));Assert.Empty(new DocumentDecisions(upgraded).History(f.Business.Owner));Assert.Equal(invoice.Hash,new QuoteBook(upgraded).Versions(f.Business.Owner).Single(v=>v.Id==invoice.Id).Hash);
        string path=Assert.Single(f.Business.Files.Snapshots());Assert.Equal(36L,TestStore.Scalar(path,"PRAGMA user_version;"));Assert.Equal(0L,TestStore.Scalar(path,"SELECT count(*) FROM DocumentDecisions;"));new BusinessStore(upgraded.DatabasePath);Assert.Single(f.Business.Files.Snapshots());
    }

    [Fact]
    public void ACancelledBookingRequiresItsCancellationReviewInsteadOfAnInvoiceVoid()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,5000);var bookings=new SarahBeauty.Business.Bookings.BookingBook(f.Business.Store);var booking=bookings.List(f.Business.Owner).Single();bookings.ChangeStatus(f.Business.Owner,booking,"Cancelled","Fictional cancelled booking");long watermark=Scalar(f,"SELECT Watermark FROM Business;");
        Assert.Throws<ArgumentException>(()=>Book(f).VoidInvoice(f.Business.Owner,Draft(invoice),BusinessFixture.Phrase,Guid.NewGuid().ToString()));Assert.Equal(watermark,Scalar(f,"SELECT Watermark FROM Business;"));Assert.Empty(Book(f).History(f.Business.Owner));
    }
}
