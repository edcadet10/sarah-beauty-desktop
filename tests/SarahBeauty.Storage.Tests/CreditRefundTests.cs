using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class CreditRefundTests
{
    private static CustomerCredit Fund(LedgerFixture f,PostedMovement receipt)
    {
        var book=new CreditBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,new CreditDraft{Name="Fictional refund funding",PaymentId=receipt.Id,
            Cents=receipt.Source.AmountCents,Date=f.Date});
        return book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }

    private static FinancialDraft Refund(LedgerFixture f,PostedMovement receipt,CustomerCredit credit,long cents,long tax=0)=>
        f.Draft(FinancialKind.Refund,cents) with{OriginalId=receipt.Id,CreditId=credit.Id,TaxCents=tax,Date=f.Date.AddDays(1)};

    private static void AssertAvailable(LedgerFixture f,PostedMovement receipt,CustomerCredit credit,ValueBalance expected)
    {
        Assert.Equal(expected,new CreditBook(f.Business.Store).Available(f.Business.Owner,credit.Id,f.Date.AddDays(2)));
        Assert.Equal(new ValueBalance(0,0),new PaymentApplications(f.Business.Store).Available(f.Business.Owner,receipt.Id,f.Date.AddDays(2)));
    }

    [Fact]
    public void CreditRefundReleasesFundingAndConsumesCashOnce()
    {
        using var f=new LedgerFixture();
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,32665));
        var credit=Fund(f,receipt);
        var draft=f.Book.SaveDraft(f.Business.Owner,Refund(f,receipt,credit,10000));
        var posted=f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(posted.Id,f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Id);
        AssertAvailable(f,receipt,credit,new(22665,0));
        Assert.Equal(22665,f.Balance("1010"));
        Assert.Equal(-22665,f.Balance("4000"));
        Assert.Equal(2L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM CreditFunding;"));
    }

    [Fact]
    public void AppliedCreditCannotAlsoBeRefunded()
    {
        using var f=new LedgerFixture();
        var invoice=ApplicationTests.Invoice(f,20000);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,32665));
        var credit=Fund(f,receipt);
        var apps=new PaymentApplications(f.Business.Store);
        var application=apps.SaveDraft(f.Business.Owner,new PaymentApplicationDraft{Name="Fictional partial credit use",CreditId=credit.Id,
            InvoiceVersionId=invoice.Id,Cents=20000,Date=f.Date});
        apps.Approve(f.Business.Owner,application.Id,application.Revision,BusinessFixture.Phrase);
        Assert.Throws<ArgumentException>(()=>f.Post(Refund(f,receipt,credit,12666)));
        f.Post(Refund(f,receipt,credit,12665));
        AssertAvailable(f,receipt,credit,new(0,0));
        Assert.Equal(20000,f.Balance("1010"));
    }

    [Fact]
    public void ReversingRefundRestoresItsCreditReservationOnce()
    {
        using var f=new LedgerFixture();
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,32665));
        var credit=Fund(f,receipt);
        var refund=f.Post(Refund(f,receipt,credit,10000));
        var reversal=f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=refund.Id,Date=f.Date.AddDays(2)});
        AssertAvailable(f,receipt,credit,new(32665,0));
        Assert.Equal(32665,f.Balance("1010"));
        Assert.Equal(credit.Id,reversal.Source.CreditId);
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=refund.Id,Date=f.Date.AddDays(2)}));
    }

    [Fact]
    public void FailedRefundRollsBackLedgerAndFundingBeforeRetry()
    {
        using var f=new LedgerFixture();
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,32665));
        var credit=Fund(f,receipt);
        var draft=f.Book.SaveDraft(f.Business.Owner,Refund(f,receipt,credit,10000));
        f.Book.FailurePoint=p=>{if(p=="credit-refund-funding")throw new IOException("Injected funding failure");};
        Assert.Throws<IOException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        AssertAvailable(f,receipt,credit,new(32665,0));
        Assert.Single(f.Book.Posted(f.Business.Owner));
        f.Book.FailurePoint=null;
        f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        AssertAvailable(f,receipt,credit,new(22665,0));
    }

    [Fact]
    public void RefundCannotReleaseAnotherReceiptsCredit()
    {
        using var f=new LedgerFixture();
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,32665));
        var other=f.Post(f.Draft(FinancialKind.Receipt,32665));
        var credit=Fund(f,receipt);
        Assert.Throws<ArgumentException>(()=>f.Post(Refund(f,other,credit,10000)));
        AssertAvailable(f,receipt,credit,new(32665,0));
    }

    [Fact]
    public void RefundAndReversalPreserveFundedTaxComponents()
    {
        using var f=new LedgerFixture();
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000});
        var credit=Fund(f,receipt);
        var refund=f.Post(Refund(f,receipt,credit,5500,500));
        AssertAvailable(f,receipt,credit,new(5500,500));
        Assert.Equal(-500,f.Balance("2000"));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=refund.Id,Date=f.Date.AddDays(2)});
        AssertAvailable(f,receipt,credit,new(11000,1000));
        Assert.Equal(-1000,f.Balance("2000"));
    }
}
