using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace SarahBeauty.Storage.Tests;

public sealed class CreditNoteTests
{
    private static CreditNoteDraft Draft(LedgerFixture f,DocumentVersion invoice,decimal quantity=1)=>new()
    {Name="Fictional agreed reduction",InvoiceVersionId=invoice.Id,IssueDate=f.Date.AddDays(1),
        Portions=[new(invoice.Snapshot.Lines.Single().ChargeId,quantity)]};

    private static DocumentVersion Credit(LedgerFixture f,CreditNoteDraft value)
    {
        var book=new CreditNoteBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,value);
        return book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }

    private static PaymentApplication Apply(LedgerFixture f,DocumentVersion invoice,DateOnly date)
    {
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000,Date=date});
        var book=new PaymentApplications(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,new PaymentApplicationDraft{Name="Fictional paid invoice",InvoiceVersionId=invoice.Id,
            PaymentId=receipt.Id,Cents=11000,Date=date});
        return Assert.Single(book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
    }

    [Fact]
    public void UnpaidChargeReductionDoesNotCreateCashIncomeOrFundedCustomerCredit()
    {
        using var f=new LedgerFixture();
        var invoice=ApplicationTests.Invoice(f,10000,0.1m);
        var book=new CreditNoteBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,Draft(f,invoice));
        var note=book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(note.Id,book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Id);
        var apps=new PaymentApplications(f.Business.Store);
        Assert.Equal(11000,apps.Outstanding(f.Business.Owner,f.Date).Sum(d=>d.Cents));
        Assert.Equal(0,apps.Outstanding(f.Business.Owner,f.Date.AddDays(1)).Sum(d=>d.Cents));
        Assert.Empty(new CreditBook(f.Business.Store).Credits(f.Business.Owner));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
        Assert.Equal(invoice.Hash,new QuoteBook(f.Business.Store).Versions(f.Business.Owner).Single(v=>v.Id==invoice.Id).Hash);
        Assert.Throws<ArgumentException>(()=>Credit(f,Draft(f,invoice,0.01m)));
        using var pdf=PdfDocument.Open(new DocumentArtifacts(f.Business.Store).Render(f.Business.Owner,note.Id));
        string text=string.Join("\n",pdf.GetPages().Select(p=>ContentOrderTextExtractor.GetText(p)));
        Assert.Contains(invoice.Snapshot.Number,text);
        Assert.Contains("Net reduction",text);
        Assert.DoesNotContain("PRIVATE-",text);
    }

    [Fact]
    public void PaidInvoiceMustBeUnappliedBeforeItsChargeCanBeCredited()
    {
        using var f=new LedgerFixture();
        var invoice=ApplicationTests.Invoice(f,10000,0.1m);
        var application=Apply(f,invoice,f.Date);
        Assert.Throws<ArgumentException>(()=>Credit(f,Draft(f,invoice)));
        var apps=new PaymentApplications(f.Business.Store);
        var reversal=apps.SaveDraft(f.Business.Owner,new PaymentApplicationDraft{Name="Fictional cancellation unapplication",
            ReversesId=application.Id,Cents=11000,Date=f.Date.AddDays(1)});
        apps.Approve(f.Business.Owner,reversal.Id,reversal.Revision,BusinessFixture.Phrase);
        Credit(f,Draft(f,invoice));
        Assert.Equal(0,apps.Outstanding(f.Business.Owner,f.Date.AddDays(1)).Sum(d=>d.Cents));
        Assert.Equal(11000,apps.Available(f.Business.Owner,application.PaymentId!,f.Date.AddDays(1)).Cents);
        Assert.Equal(11000,f.Balance("1010"));
        Assert.Equal(-10000,f.Balance("4000"));
    }

    [Fact]
    public void ABackdatedCreditCannotConsumeValuePromisedToALaterApplication()
    {
        using var f=new LedgerFixture();
        var invoice=ApplicationTests.Invoice(f,10000,0.1m);
        Apply(f,invoice,f.Date.AddDays(2));
        Assert.Throws<ArgumentException>(()=>Credit(f,Draft(f,invoice)));
        Assert.DoesNotContain(new QuoteBook(f.Business.Store).Versions(f.Business.Owner),v=>v.Snapshot.Kind==DocumentKind.CreditNote);
        Assert.Equal(0,new PaymentApplications(f.Business.Store).Outstanding(f.Business.Owner,f.Date.AddDays(2)).Sum(d=>d.Cents));
    }

    [Fact]
    public void FractionalCreditsKeepTheFinalCentResidualAndReleaseSourceCoverage()
    {
        using var f=new LedgerFixture();
        var invoice=ApplicationTests.Invoice(f,2);
        long total=0;
        foreach(decimal quantity in new[]{0.3m,0.3m,0.3m,0.1m})total+=Credit(f,Draft(f,invoice,quantity)).Snapshot.TotalCents;
        Assert.Equal(2,total);
        var quote=new QuoteBook(f.Business.Store).Versions(f.Business.Owner).Single(v=>v.Snapshot.Kind==DocumentKind.Quote);
        var remaining=new InvoiceBook(f.Business.Store).Remaining(f.Business.Owner,quote.Id).Single();
        Assert.Equal(1,remaining.Quantity);
        Assert.Equal(2,remaining.NetCents);
    }

    [Fact]
    public void FullyCreditedChargesCanBeAmendedAndReissuedWithoutDuplicateMilestones()
    {
        using var f=new LedgerFixture();
        var invoice=ApplicationTests.Invoice(f,10000,0.1m);
        Credit(f,Draft(f,invoice));
        var bookings=new BookingBook(f.Business.Store);
        var booking=bookings.List(f.Business.Owner).Single();
        booking=bookings.Save(f.Business.Owner,booking with{Charges=booking.Charges.Select(c=>c with{UnitCents=12000}).ToArray()});
        var quotes=new QuoteBook(f.Business.Store);
        var old=quotes.Versions(f.Business.Owner).Single(v=>v.Snapshot.Kind==DocumentKind.Quote);
        var draft=quotes.SaveDraft(f.Business.Owner,quotes.FromBooking(f.Business.Owner,booking,old.DocumentId) with{IssueDate=f.Date.AddDays(1)});
        var quote=quotes.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        quotes.Accept(f.Business.Owner,quote.Id,f.Date.AddDays(1),"Fictional amended agreement after credit");
        var invoices=new InvoiceBook(f.Business.Store);
        var revised=invoices.SaveDraft(f.Business.Owner,new InvoiceDraft{Name="Fictional corrected invoice",QuoteVersionId=quote.Id,
            PayerCustomerId=f.CustomerId,IssueDate=f.Date.AddDays(1),Portions=quote.Snapshot.Lines.Select(l=>new InvoicePortion(l.ChargeId,l.Quantity)).ToArray()});
        var issued=invoices.Issue(f.Business.Owner,revised.Id,revised.Revision,BusinessFixture.Phrase);
        Assert.Equal(13200,issued.Snapshot.Milestones.Sum(m=>m.Cents));
        Assert.Equal(13200,new PaymentApplications(f.Business.Store).Outstanding(f.Business.Owner,f.Date.AddDays(1)).Sum(d=>d.Cents));
    }

    [Theory]
    [InlineData("credit-note-reductions")]
    [InlineData("credit-note-audit")]
    public void FailureRollsBackTheNumberDocumentCoverageAndBalance(string stage)
    {
        using var f=new LedgerFixture();
        var invoice=ApplicationTests.Invoice(f,10000,0.1m);
        var book=new CreditNoteBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,Draft(f,invoice));
        book.FailurePoint=p=>{if(p==stage)throw new IOException("Injected credit note failure");};
        Assert.Throws<IOException>(()=>book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Equal(11000,new PaymentApplications(f.Business.Store).Outstanding(f.Business.Owner,f.Date.AddDays(1)).Sum(d=>d.Cents));
        Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM CreditNoteHeaders;"));
        book.FailurePoint=null;
        Assert.Equal("C-000001",book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Snapshot.Number);
    }

    [Fact]
    public void CompetingSavedCreditsCannotReduceTheSameInvoiceTwice()
    {
        using var f=new LedgerFixture();
        var invoice=ApplicationTests.Invoice(f,10000,0.1m);
        var book=new CreditNoteBook(f.Business.Store);
        var first=book.SaveDraft(f.Business.Owner,Draft(f,invoice,0.6m));
        var second=book.SaveDraft(f.Business.Owner,Draft(f,invoice,0.6m));
        book.Issue(f.Business.Owner,first.Id,first.Revision,BusinessFixture.Phrase);
        Assert.Throws<ArgumentException>(()=>book.Issue(f.Business.Owner,second.Id,second.Revision,BusinessFixture.Phrase));
        var balance=new PaymentApplications(f.Business.Store).Outstanding(f.Business.Owner,f.Date.AddDays(1));
        Assert.Equal(4400,balance.Sum(d=>d.Cents));
        Assert.Equal(400,balance.Sum(d=>d.TaxCents));
    }
}
