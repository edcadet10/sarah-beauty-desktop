using System.Text.Json;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace SarahBeauty.Storage.Tests;

public sealed class ReceiptTests
{
    private static ReceiptDraft Draft(LedgerFixture f,string payment)=>new(){Name="Fictional receipt",PaymentId=payment,
        IssueDate=f.Date,ClientReference="Fictional client reference"};

    private static PaymentApplication Apply(LedgerFixture f,DocumentVersion invoice,long cents,string payment="",string credit="")
    {
        var book=new PaymentApplications(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,new PaymentApplicationDraft{Name="Fictional receipt application",InvoiceVersionId=invoice.Id,
            PaymentId=payment,CreditId=credit,Cents=cents,Date=f.Date});
        return Assert.Single(book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
    }

    [Fact]
    public void ApprovedGrossPaymentAndBalancesAreFrozenWithoutLeakingInternalEvidence()
    {
        using var f=new LedgerFixture();
        var invoice=ApplicationTests.Invoice(f,10000);
        var payment=f.Post(f.Draft(FinancialKind.Receipt,12000) with{FeeCents=360,PaymentMethod="Card",Name="PRIVATE-MEMO",
            ExternalReference="PRIVATE-REFERENCE",EvidenceId="PRIVATE-EVIDENCE"});
        var application=Apply(f,invoice,8000,payment:payment.Id);
        var book=new ReceiptBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,Draft(f,payment.Id));
        var receipt=book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        var detail=receipt.Snapshot.Receipt!;
        Assert.Equal(12000,receipt.Snapshot.TotalCents);
        Assert.Equal(4000,detail.PaymentAvailableCents);
        Assert.Equal(2000,detail.CustomerInvoiceBalanceCents);
        Assert.Equal(8000,Assert.Single(detail.Applications).Cents);
        Assert.DoesNotContain("PRIVATE-",JsonSerializer.Serialize(receipt.Snapshot));
        var artifacts=new DocumentArtifacts(f.Business.Store);
        string path=artifacts.Render(f.Business.Owner,receipt.Id);
        using(var pdf=PdfDocument.Open(path))
        {
            string text=string.Join("\n",pdf.GetPages().Select(p=>ContentOrderTextExtractor.GetText(p)));
            Assert.Contains("Gross payment: $120.00",text);
            Assert.Contains("Card",text);
            Assert.Contains(invoice.Snapshot.Number,text);
            Assert.DoesNotContain("PRIVATE-",text);
        }
        var apps=new PaymentApplications(f.Business.Store);
        var reversal=apps.SaveDraft(f.Business.Owner,new PaymentApplicationDraft{Name="Fictional later unapplication",ReversesId=application.Id,Cents=2000,Date=f.Date.AddDays(1)});
        apps.Approve(f.Business.Owner,reversal.Id,reversal.Revision,BusinessFixture.Phrase);
        f.Post(f.Draft(FinancialKind.Refund,2000) with{OriginalId=payment.Id,Date=f.Date.AddDays(1)});
        Assert.Equal(receipt.Id,book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Id);
        Assert.Equal(path,artifacts.Render(f.Business.Owner,receipt.Id));
        Assert.Equal(4000,new QuoteBook(f.Business.Store).Versions(f.Business.Owner).Single(v=>v.Id==receipt.Id).Snapshot.Receipt!.PaymentAvailableCents);
        Assert.Equal(9640,f.Balance("1010"));
    }

    [Fact]
    public void ReceiptIncludesCreditApplicationsWithoutCountingReservedValueTwice()
    {
        using var f=new LedgerFixture();
        var invoice=ApplicationTests.Invoice(f,10000);
        var payment=f.Post(f.Draft(FinancialKind.Receipt,12000));
        var credits=new CreditBook(f.Business.Store);
        var funding=credits.SaveDraft(f.Business.Owner,new CreditDraft{Name="Fictional receipt credit",PaymentId=payment.Id,Cents=12000,Date=f.Date});
        var credit=credits.Approve(f.Business.Owner,funding.Id,funding.Revision,BusinessFixture.Phrase);
        Apply(f,invoice,5000,credit:credit.Id);
        var book=new ReceiptBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,Draft(f,payment.Id));
        var detail=book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Snapshot.Receipt!;
        Assert.Equal(0,detail.PaymentAvailableCents);
        Assert.Equal(7000,detail.UnusedFundedCreditCents);
        Assert.Equal(5000,Assert.Single(detail.Applications).Cents);
        Assert.Equal("Not recorded",detail.Method);
    }

    [Fact]
    public void ChangedRecordsRequireFreshReviewAndAnotherDraftCannotIssueASecondReceipt()
    {
        using var f=new LedgerFixture();
        var payment=f.Post(f.Draft(FinancialKind.Receipt,12000));
        var book=new ReceiptBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,Draft(f,payment.Id));
        new CustomerBook(f.Business.Store).Save(f.Business.Owner,new Customer{FullName="Fictional change after receipt review"});
        Assert.Throws<ArgumentException>(()=>book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        draft=book.SaveDraft(f.Business.Owner,draft);
        book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        var duplicate=book.SaveDraft(f.Business.Owner,Draft(f,payment.Id));
        Assert.Throws<ArgumentException>(()=>book.Issue(f.Business.Owner,duplicate.Id,duplicate.Revision,BusinessFixture.Phrase));
        Assert.Equal(1L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM ReceiptHeaders;"));
    }

    [Fact]
    public void ReversedOrUnapprovedPaymentsCannotProduceAReceipt()
    {
        using var f=new LedgerFixture();
        var book=new ReceiptBook(f.Business.Store);
        var pending=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Receipt,12000));
        Assert.Throws<ArgumentException>(()=>book.SaveDraft(f.Business.Owner,Draft(f,pending.Id)));
        var payment=f.Post(f.Draft(FinancialKind.Receipt,12000));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=payment.Id});
        var draft=book.SaveDraft(f.Business.Owner,Draft(f,payment.Id));
        Assert.Throws<ArgumentException>(()=>book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
    }

    [Fact]
    public void FailedReceiptIssueRollsBackAndRetriesWithOneNumber()
    {
        using var f=new LedgerFixture();
        var payment=f.Post(f.Draft(FinancialKind.Receipt,12000));
        var book=new ReceiptBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,Draft(f,payment.Id));
        book.FailurePoint=_=>throw new IOException("Injected receipt issue failure");
        Assert.Throws<IOException>(()=>book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM ReceiptHeaders;"));
        book.FailurePoint=null;
        Assert.Equal("R-000001",book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Snapshot.Number);
        Assert.Single(f.Book.Posted(f.Business.Owner));
    }
}
