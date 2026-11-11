using System.Text.Json;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReceiptClassificationTests
{
    private static PostedMovement Unknown(LedgerFixture f,long cents)=>f.Post(f.Draft(FinancialKind.UnclassifiedReceipt,cents) with{PaymentMethod="Fictional bank transfer",Name="PRIVATE-UNKNOWN"});
    private static FinancialDraft Classify(LedgerFixture f,PostedMovement source,long cents,long tax=0)=>
        f.Draft(FinancialKind.ReceiptClassification,cents) with{OriginalId=source.Id,CustomerId=f.CustomerId,CounterAccountId=f.Accounts["4000"].Id,
            TaxCents=tax,EvidenceId="PRIVATE-CLASSIFICATION-REVIEW"};
    private static PaymentApplicationDraft Application(LedgerFixture f,string payment,DocumentVersion invoice,long cents)=>
        new(){Name="Fictional classified allocation",PaymentId=payment,InvoiceVersionId=invoice.Id,Cents=cents,Date=f.Date};

    [Fact]
    public void ReviewedUnclassifiedValuePaysAnInvoiceWithoutAnotherCashReceipt()
    {
        using var f=new LedgerFixture();
        var invoice=ApplicationTests.Invoice(f,5000,0.1m);
        var unknown=Unknown(f,11000);
        var apps=new PaymentApplications(f.Business.Store);
        var invalid=apps.SaveDraft(f.Business.Owner,Application(f,unknown.Id,invoice,5500));
        Assert.Throws<ArgumentException>(()=>apps.Approve(f.Business.Owner,invalid.Id,invalid.Revision,BusinessFixture.Phrase));
        var classified=f.Post(Classify(f,unknown,5500,500));
        Assert.Equal(11000,f.Balance("1010"));
        Assert.Equal(-5500,f.Balance("2400"));
        Assert.Equal(-5000,f.Balance("4000"));
        Assert.Equal(-500,f.Balance("2000"));
        var applied=apps.SaveDraft(f.Business.Owner,Application(f,classified.Id,invoice,5500));
        apps.Approve(f.Business.Owner,applied.Id,applied.Revision,BusinessFixture.Phrase);
        Assert.Equal(0,apps.Outstanding(f.Business.Owner,f.Date).Sum(d=>d.Cents));
        Assert.Equal(0,apps.Available(f.Business.Owner,classified.Id,f.Date).Cents);
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=classified.Id}));
        Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
    }

    [Fact]
    public void HeldTaxCarriesForwardOnceAndHeldRefundCannotSpendClassifiedValue()
    {
        using var f=new LedgerFixture();
        var held=f.Post(f.Draft(FinancialKind.Receipt,11000) with{CounterAccountId=f.Accounts["2200"].Id,TaxCents=1000,FeeCents=360});
        var classified=f.Post(Classify(f,held,5500,500));
        Assert.Equal(500,classified.Source.SourceTaxCents);
        Assert.Equal(10640,f.Balance("1010"));
        Assert.Equal(-5000,f.Balance("2200"));
        Assert.Equal(-5000,f.Balance("4000"));
        Assert.Equal(-1000,f.Balance("2000"));
        var report=new FinancialReports(f.Business.Store).Load(f.Business.Owner,f.Date,f.Date);
        Assert.Equal(1000,report.Tax.CollectedCents);
        Assert.Equal(5500,Assert.Single(report.Customers).HeldReceiptsCents);
        Assert.Equal(5500,Assert.Single(report.Customers).UnallocatedReceiptsCents);
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Refund,5501) with{OriginalId=held.Id,TaxCents=500,CounterAccountId=f.Accounts["2200"].Id}));
        f.Post(f.Draft(FinancialKind.Refund,5500) with{OriginalId=held.Id,TaxCents=500,CounterAccountId=f.Accounts["2200"].Id});
        f.Post(f.Draft(FinancialKind.Refund,5500) with{OriginalId=classified.Id,TaxCents=500});
        Assert.Equal(-360,f.Balance("1010"));
        Assert.Equal(0,f.Balance("2200"));
        Assert.Equal(0,f.Balance("2000"));
        Assert.Equal(0,f.Balance("4000"));
        Assert.Equal(360,f.Balance("5020"));
    }

    [Fact]
    public void UnknownReceiptCanBeReturnedWithoutInventingIncomeOrAClient()
    {
        using var f=new LedgerFixture();
        var unknown=Unknown(f,11000);
        f.Post(f.Draft(FinancialKind.Refund,11000) with{OriginalId=unknown.Id,CustomerId="",CounterAccountId=f.Accounts["2400"].Id});
        Assert.Equal(0,f.Balance("1010"));
        Assert.Equal(0,f.Balance("2400"));
        Assert.Equal(0,f.Balance("4000"));
        Assert.Throws<ArgumentException>(()=>f.Post(Classify(f,unknown,1)));
    }

    [Fact]
    public void ClassifiedValueCanFundCreditAndPreventsUndoUntilFundingIsReleased()
    {
        using var f=new LedgerFixture();
        var unknown=Unknown(f,32665);
        var classified=f.Post(Classify(f,unknown,32665));
        var credits=new CreditBook(f.Business.Store);
        var draft=credits.SaveDraft(f.Business.Owner,new CreditDraft{Name="Fictional classified credit",PaymentId=classified.Id,Cents=32665,Date=f.Date});
        var credit=credits.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(32665,credits.Available(f.Business.Owner,credit.Id,f.Date).Cents);
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=classified.Id}));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=unknown.Id}));
        Assert.Equal(32665,f.Balance("1010"));
        Assert.Equal(-32665,f.Balance("4000"));
    }

    [Fact]
    public void StaleAndBackdatedClassificationsCannotSpendOriginalValueTwice()
    {
        using var f=new LedgerFixture();
        var unknown=Unknown(f,10000);
        var first=f.Book.SaveDraft(f.Business.Owner,Classify(f,unknown,6000) with{Date=f.Date.AddDays(2)});
        var stale=f.Book.SaveDraft(f.Business.Owner,Classify(f,unknown,6000));
        f.Book.Approve(f.Business.Owner,first.Id,first.Revision,BusinessFixture.Phrase);
        Assert.Throws<ArgumentException>(()=>f.Book.Approve(f.Business.Owner,stale.Id,stale.Revision,BusinessFixture.Phrase));
        Assert.Throws<ArgumentException>(()=>f.Post(Classify(f,unknown,6000) with{Date=f.Date.AddDays(1)}));
        f.Post(Classify(f,unknown,4000) with{Date=f.Date.AddDays(1)});
        Assert.Equal(0,f.Balance("2400"));
        Assert.Equal(10000,f.Balance("1010"));
        Assert.Equal(-10000,f.Balance("4000"));
    }

    [Fact]
    public void FailureRollsBackRecognitionAndRetryPostsOnce()
    {
        using var f=new LedgerFixture();
        var unknown=Unknown(f,11000);
        var draft=f.Book.SaveDraft(f.Business.Owner,Classify(f,unknown,11000,1000));
        f.Book.FailurePoint=p=>{if(p=="receipt-classification")throw new IOException("Injected classification failure");};
        Assert.Throws<IOException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Equal(-11000,f.Balance("2400"));
        Assert.Equal(0,f.Balance("4000"));
        f.Book.FailurePoint=null;
        var result=f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(result.Id,f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Id);
        Assert.Equal(-10000,f.Balance("4000"));
        Assert.Equal(11000,f.Balance("1010"));
    }

    [Fact]
    public void ClassificationReceiptAcknowledgesItsPortionAndActualOriginalPaymentDate()
    {
        using var f=new LedgerFixture();
        var unknown=Unknown(f,11000);
        var classified=f.Post(Classify(f,unknown,5500,500) with{Date=f.Date.AddDays(2)});
        var receipts=new ReceiptBook(f.Business.Store);
        var draft=receipts.SaveDraft(f.Business.Owner,new ReceiptDraft{Name="Fictional classified portion",PaymentId=classified.Id,IssueDate=f.Date.AddDays(2)});
        var receipt=receipts.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(f.Date,receipt.Snapshot.Receipt!.PaymentDate);
        Assert.Equal("Fictional bank transfer",receipt.Snapshot.Receipt.Method);
        Assert.Equal(5500,receipt.Snapshot.TotalCents);
        Assert.Contains("does not acknowledge an additional payment",receipt.Snapshot.Terms);
        Assert.DoesNotContain("PRIVATE-",JsonSerializer.Serialize(receipt.Snapshot));
        Assert.Equal(11000,f.Balance("1010"));
        Assert.Equal(5000,new FinancialReports(f.Business.Store).Load(f.Business.Owner,f.Date.AddDays(1),f.Date.AddDays(2)).ProfitLoss.IncomeCents);
        Assert.Equal(0,new FinancialReports(f.Business.Store).Load(f.Business.Owner,f.Date,f.Date).ProfitLoss.IncomeCents);
    }

    [Fact]
    public void TaxOnlyHeldValueHasNoNewMonetaryEntryAndKeepsItsReversalHistory()
    {
        using var f=new LedgerFixture();
        var held=f.Post(f.Draft(FinancialKind.Receipt,1000) with{CounterAccountId=f.Accounts["2200"].Id,TaxCents=1000});
        var classified=f.Post(Classify(f,held,1000,1000));
        Assert.Empty(f.Book.Journal(f.Business.Owner,classified.Id));
        Assert.Equal(-1000,f.Balance("2000"));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=classified.Id});
        Assert.Equal(1000,new PaymentApplications(f.Business.Store).Available(f.Business.Owner,held.Id,f.Date).Cents);
    }

    [Fact]
    public void OrdinaryIncomeCannotBeClassifiedAgainAndHeldTaxCannotSilentlyDisappear()
    {
        using var f=new LedgerFixture();
        var income=f.Post(f.Draft(FinancialKind.Receipt,11000));
        Assert.Throws<ArgumentException>(()=>f.Post(Classify(f,income,5500)));
        var held=f.Post(f.Draft(FinancialKind.Receipt,11000) with{CounterAccountId=f.Accounts["2200"].Id,TaxCents=1000});
        Assert.Throws<ArgumentException>(()=>f.Post(Classify(f,held,5500,499)));
        Assert.Throws<ArgumentException>(()=>f.Post(Classify(f,held,5500,500) with{EvidenceId=""}));
        Assert.Throws<ArgumentException>(()=>f.Post(Classify(f,held,5500,500) with{TaxResolved=false}));
        Assert.Throws<ArgumentException>(()=>f.Post(Classify(f,held,5500,500) with{ReliableReference=true,Provider="Fictional",ExternalReference="test"}));
        Assert.Equal(-1000,f.Balance("2000"));
        Assert.Equal(-11000,f.Balance("4000"));
    }
}
