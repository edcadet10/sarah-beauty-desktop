using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class PostingTests
{
    [Fact]
    public void AgreedBookkeepingFixtureBalancesWithoutTreatingFinancingAsIncome()
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.Contribution,100000));
        f.Post(f.Draft(FinancialKind.LoanProceeds,50000));
        f.Post(f.Draft(FinancialKind.Receipt,20000));
        f.Post(f.Draft(FinancialKind.Expense,8000));
        f.Post(f.Draft(FinancialKind.AssetPurchase,30000));
        f.Post(f.Draft(FinancialKind.LoanPayment,10000) with {PrincipalCents=10000});
        f.Post(f.Draft(FinancialKind.Withdrawal,5000));
        Assert.Equal(117000,f.Balance("1010"));
        Assert.Equal(30000,f.Balance("1500"));
        Assert.Equal(-40000,f.Balance("2300"));
        var reports=new LedgerReports(f.Business.Store);
        Assert.Equal(12000,reports.ProfitLoss(f.Business.Owner,DateOnly.MinValue,DateOnly.MaxValue).ResultCents);
        var sheet=reports.BalanceSheet(f.Business.Owner,DateOnly.MaxValue);
        Assert.Equal(new BalanceSheet(147000,40000,107000),sheet);
        Assert.True(sheet.Balances);
    }

    [Fact]
    public void GrossReceiptFeeAndPayoutDoNotCreateAnotherSale()
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.Receipt,12000) with {CashAccountId=f.Accounts["1020"].Id,FeeCents=360});
        f.Post(f.Draft(FinancialKind.Transfer,11640) with {CashAccountId=f.Accounts["1020"].Id,ToAccountId=f.Accounts["1010"].Id});
        Assert.Equal(0,f.Balance("1020"));
        Assert.Equal(11640,f.Balance("1010"));
        Assert.Equal(-12000,f.Balance("4000"));
        Assert.Equal(360,f.Balance("5020"));
    }

    [Theory]
    [InlineData("batch")]
    [InlineData("movement")]
    [InlineData("line")]
    [InlineData("audit")]
    public void FailureAtEachWriteStageRollsBackAndRetryPostsOnce(string stage)
    {
        using var f=new LedgerFixture();
        var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Expense,8000));
        f.Book.FailurePoint=point=>{if(point==stage)throw new IOException("Injected failure");};
        Assert.Throws<IOException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
        Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM JournalLines;"));
        Assert.Equal("Draft",f.Book.Drafts(f.Business.Owner).Single().Status);
        f.Book.FailurePoint=null;
        var posted=f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(posted.Id,f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Id);
        Assert.Single(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void StaleDraftAndWrongPassphraseCannotApprove()
    {
        using var f=new LedgerFixture();
        var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Expense,8000));
        var edited=f.Book.SaveDraft(f.Business.Owner,draft with {AmountCents=9000});
        Assert.Throws<InvalidOperationException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Throws<UnauthorizedAccessException>(()=>f.Book.Approve(f.Business.Owner,edited.Id,edited.Revision,"incorrect"));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void ReliableReferenceIsUniqueWhileTwoLegitimateCashReceiptsCanRemainSeparate()
    {
        using var f=new LedgerFixture();
        var value=f.Draft(FinancialKind.Receipt,12000) with {ReliableReference=true,Provider="Example provider",ExternalReference="EXAMPLE-001"};
        f.Post(value);
        Assert.Throws<ArgumentException>(()=>f.Post(value));
        f.Post(value with {ReliableReference=false,ExternalReference=""});
        Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
    }

    [Fact]
    public void OverpaymentAndBackdatedPrincipalPaymentCannotMakeDebtNegative()
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.LoanProceeds,50000));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.LoanPayment,50001) with {PrincipalCents=50001}));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.LoanPayment,10000) with {PrincipalCents=10000,Date=f.Date.AddDays(-1)}));
        Assert.Equal(-50000,f.Balance("2300"));
    }

    [Fact]
    public void OwnerReimbursementDoesNotRecognizeTheExpenseTwice()
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.OwnerPaidExpense,8000));
        f.Post(f.Draft(FinancialKind.OwnerReimbursement,8000));
        Assert.Equal(8000,f.Balance("5010"));
        Assert.Equal(0,f.Balance("2100"));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.OwnerReimbursement,1)));
    }

    [Fact]
    public void RefundsAreBoundedAndReversalsPreserveExactJournalCents()
    {
        using var f=new LedgerFixture();
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,12000));
        var refund=f.Post(f.Draft(FinancialKind.Refund,8000) with {OriginalId=receipt.Id});
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Refund,4001) with {OriginalId=receipt.Id}));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Reversal,0) with {OriginalId=receipt.Id}));
        f.Post(f.Draft(FinancialKind.Reversal,0) with {OriginalId=refund.Id});
        f.Post(f.Draft(FinancialKind.Reversal,0) with {OriginalId=receipt.Id});
        Assert.Equal(0,f.Balance("1010"));
        Assert.Equal(0,f.Balance("4000"));
    }

    [Fact]
    public void DraftsDoNotAffectReportsAndClosedDatesCannotPost()
    {
        using var f=new LedgerFixture();
        f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Receipt,99999));
        Assert.Equal(0,f.Balance("4000"));
        f.Business.Files.Execute("UPDATE BookSettings SET ClosedThrough='2027-01-31';");
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Expense,8000)));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public async Task ConcurrentApprovalsCreateOneBatchAndEitherOwnerCanApprove()
    {
        using var f=new LedgerFixture();
        var access=new SarahBeauty.Business.Security.OwnerAccess(f.Business.Store);
        var second=access.Owners().Single(o=>o.Id!=f.Business.Owner.OwnerId);
        var owner=access.SignIn(second.Id,BusinessFixture.Phrase);
        var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Expense,8000));
        var results=await Task.WhenAll(
            Task.Run(()=>f.Book.Approve(owner,draft.Id,draft.Revision,BusinessFixture.Phrase)),
            Task.Run(()=>f.Book.Approve(owner,draft.Id,draft.Revision,BusinessFixture.Phrase)));
        Assert.Equal(results[0].Id,results[1].Id);
        Assert.Equal(owner.OwnerId,results[0].ApprovedBy);
        Assert.Single(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void LaterRefundDoesNotLeakIntoAnEarlierReport()
    {
        using var f=new LedgerFixture();
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,12000));
        f.Post(f.Draft(FinancialKind.Refund,4000) with {OriginalId=receipt.Id,Date=new DateOnly(2027,2,10)});
        var reports=new LedgerReports(f.Business.Store);
        Assert.Equal(12000,reports.ProfitLoss(f.Business.Owner,new(2027,1,1),new(2027,1,31)).IncomeCents);
        Assert.Equal(-4000,reports.ProfitLoss(f.Business.Owner,new(2027,2,1),new(2027,2,28)).IncomeCents);
        Assert.Equal(12000,reports.BalanceSheet(f.Business.Owner,new(2027,1,31)).AssetsCents);
    }

    [Fact]
    public void DepreciationCannotExceedApprovedCarryingValue()
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.AssetPurchase,30000));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Depreciation,30001)));
        f.Post(f.Draft(FinancialKind.Depreciation,10000));
        Assert.Equal(-10000,f.Balance("1590"));
        Assert.Equal(20000,new LedgerReports(f.Business.Store).BalanceSheet(f.Business.Owner,f.Date).AssetsCents-f.Balance("1010"));
    }
}
