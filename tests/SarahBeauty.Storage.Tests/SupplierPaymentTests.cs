using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Purchasing;

namespace SarahBeauty.Storage.Tests;

public sealed class SupplierPaymentTests
{
    private static Purchase Bill(LedgerFixture f,Purchase? value=null)
    {
        var book=new PurchaseBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,value??PurchaseTests.Draft(f));
        return book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }

    private static FinancialDraft Payment(LedgerFixture f,Purchase bill,long cents,string funding="Business cash")=>
        f.Draft(FinancialKind.PurchasePayment,cents) with{PurchaseId=bill.Id,PurchaseFunding=funding,EconomicOwnerId=f.Business.Owner.OwnerId};

    private static long Due(LedgerFixture f,Purchase bill,DateOnly? date=null)=>
        new PurchasePayments(f.Business.Store).Outstanding(f.Business.Owner,date??f.Date).Single(p=>p.PurchaseId==bill.Id).Cents;

    [Fact]
    public void PartialAndFinalCashPaymentsRecognizeBusinessSharesOnceAndSeparatePersonalDraw()
    {
        using var f=new LedgerFixture();
        var bill=Bill(f);
        f.Post(Payment(f,bill,17600));
        Assert.Equal(17600,Due(f,bill));
        Assert.Equal(550,f.Balance("5000"));
        Assert.Equal(16500,f.Balance("1500"));
        Assert.Equal(550,f.Balance("3100"));
        f.Post(Payment(f,bill,17600));
        Assert.Equal(0,Due(f,bill));
        Assert.Equal(1100,f.Balance("5000"));
        Assert.Equal(33000,f.Balance("1500"));
        Assert.Equal(1100,f.Balance("3100"));
        Assert.Equal(-35200,f.Balance("1010"));
        Assert.Equal(0,f.Balance("2000"));
        Assert.Equal(0,Assert.Single(new FinancialReports(f.Business.Store).Load(f.Business.Owner,f.Date,f.Date).Suppliers).OpenCents);
        Assert.Throws<ArgumentException>(()=>f.Post(Payment(f,bill,1)));
    }

    [Theory]
    [InlineData("Owner reimbursable","2100")]
    [InlineData("Owner contribution","3000")]
    public void OwnerFundingExcludesPersonalCostsAndDoesNotCreateBusinessCash(string funding,string account)
    {
        using var f=new LedgerFixture();
        var bill=Bill(f);
        var payment=f.Post(Payment(f,bill,35200,funding) with{CashAccountId=""});
        Assert.Equal(0,Due(f,bill));
        Assert.Equal(-34100,f.Balance(account));
        Assert.Equal(0,f.Balance("3100"));
        Assert.Equal(0,f.Balance("1010"));
        if(account!="2100")return;
        f.Post(f.Draft(FinancialKind.OwnerReimbursement,34100));
        Assert.Equal(1100,f.Balance("5000"));
        Assert.Equal(0,f.Balance("2100"));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=payment.Id}));
    }

    [Fact]
    public void ReversalRestoresBillAvailabilityAndEveryAssetLineWithoutEditingTheBill()
    {
        using var f=new LedgerFixture();
        var bill=Bill(f);
        var payment=f.Post(Payment(f,bill,35200));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=payment.Id,Date=f.Date.AddDays(1)});
        Assert.Equal(0,Due(f,bill));
        Assert.Equal(35200,Due(f,bill,f.Date.AddDays(1)));
        Assert.Equal(0,f.Balance("1500"));
        Assert.Equal(0,f.Balance("5000"));
        Assert.Equal(bill.Revision,new PurchaseBook(f.Business.Store).List(f.Business.Owner).Single().Revision);
    }

    [Fact]
    public async Task CompetingPaymentsCannotConsumeTheSameUnpaidBillTwice()
    {
        using var f=new LedgerFixture();
        var bill=Bill(f);
        var drafts=new[]{f.Book.SaveDraft(f.Business.Owner,Payment(f,bill,20000)),f.Book.SaveDraft(f.Business.Owner,Payment(f,bill,20000))};
        var results=await Task.WhenAll(drafts.Select(d=>Task.Run(()=>
        {
            try{f.Book.Approve(f.Business.Owner,d.Id,d.Revision,BusinessFixture.Phrase);return true;}
            catch(ArgumentException){return false;}
        })));
        Assert.Single(results,r=>r);
        Assert.Equal(15200,Due(f,bill));
        Assert.Single(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void FailureAfterSettlementRollsBackCashCostsAndBillBalanceBeforeOneRetry()
    {
        using var f=new LedgerFixture();
        var bill=Bill(f);
        var draft=f.Book.SaveDraft(f.Business.Owner,Payment(f,bill,35200));
        f.Book.FailurePoint=p=>{if(p=="purchase-settlement")throw new IOException("Injected supplier payment failure");};
        Assert.Throws<IOException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Equal(35200,Due(f,bill));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
        f.Book.FailurePoint=null;
        var payment=f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(payment.Id,f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Id);
        Assert.Equal(0,Due(f,bill));
    }

    [Fact]
    public void OwnerFundedPersonalOnlyInstallmentCanSettleAndReverseWithoutAFakeBusinessEntry()
    {
        using var f=new LedgerFixture();
        var value=PurchaseTests.Draft(f);
        var bill=Bill(f,value with{Lines=[new(Guid.NewGuid().ToString(),"Fictional personal portion",1,1,0,0m,f.Accounts["5000"].Id),
            new(Guid.NewGuid().ToString(),"Fictional business portion",1,2,0,0.5m,f.Accounts["5000"].Id)]});
        var payment=f.Post(Payment(f,bill,1,"Owner contribution") with{Date=f.Date.AddDays(1)});
        Assert.Empty(f.Book.Journal(f.Business.Owner,payment.Id));
        Assert.Equal(2,Due(f,bill,f.Date.AddDays(1)));
        Assert.Throws<ArgumentException>(()=>f.Post(Payment(f,bill,2,"Owner contribution")));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=payment.Id,Date=f.Date.AddDays(2)});
        Assert.Equal(3,Due(f,bill,f.Date.AddDays(2)));
        Assert.Equal(0,f.Balance("3000"));
    }
}
