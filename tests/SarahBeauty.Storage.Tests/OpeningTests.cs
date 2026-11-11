using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class OpeningTests
{
    private static FinancialDraft Opening(LedgerFixture f,long cents,string code="1010")=>f.Draft(FinancialKind.Opening,cents) with
    {CounterAccountId=f.Accounts[code].Id,EvidenceId="Fictional opening evidence",OpeningCredit=code=="2300"};

    [Fact]
    public void ConfirmedZeroIsDistinctFromUnknownAndRevocationRetainsHistory()
    {
        using var f=new LedgerFixture();
        var book=new OpeningBalances(f.Business.Store);
        var chart=new ChartOfAccounts(f.Business.Store);
        var bank=f.Accounts["1010"];
        Assert.False(bank.OpeningConfirmed);
        Assert.Throws<ArgumentException>(()=>chart.Save(f.Business.Owner,bank with{OpeningConfirmed=true}));
        book.ConfirmZero(f.Business.Owner,bank.Id,f.Date,"Fictional zero-opening review",BusinessFixture.Phrase);
        Assert.True(chart.List(f.Business.Owner).Single(a=>a.Id==bank.Id).OpeningConfirmed);
        Assert.Empty(f.Book.Posted(f.Business.Owner));
        var opening=Assert.Single(book.List(f.Business.Owner));
        book.RevokeZero(f.Business.Owner,opening.Id,"Fictional correction to date",BusinessFixture.Phrase);
        Assert.NotNull(Assert.Single(book.List(f.Business.Owner)).RevokedAt);
        Assert.False(chart.List(f.Business.Owner).Single(a=>a.Id==bank.Id).OpeningConfirmed);
    }

    [Fact]
    public void EvidencedCashOpeningPostsOnceAndReversalRestoresUnknownStatus()
    {
        using var f=new LedgerFixture();
        var opening=f.Post(Opening(f,100000));
        Assert.Equal(100000,f.Balance("1010"));
        Assert.Throws<ArgumentException>(()=>f.Post(Opening(f,100000)));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=opening.Id,Date=f.Date.AddDays(1)});
        Assert.Equal(0,f.Balance("1010"));
        Assert.NotNull(Assert.Single(new OpeningBalances(f.Business.Store).List(f.Business.Owner)).RevokedAt);
        Assert.False(new ChartOfAccounts(f.Business.Store).List(f.Business.Owner).Single(a=>a.Code=="1010").OpeningConfirmed);
    }

    [Fact]
    public void NonzeroLoanMetadataNeedsItsMatchingApprovedOpeningBeforeRepayment()
    {
        using var f=new LedgerFixture();
        var loan=new BusinessResources(f.Business.Store).SaveLoan(f.Business.Owner,new Loan{Name="Fictional starting loan",OpeningDate=f.Date,OpeningPrincipalCents=10000});
        var repayment=f.Draft(FinancialKind.LoanPayment,2000) with{LoanId=loan.Id,PrincipalCents=2000};
        Assert.Throws<ArgumentException>(()=>f.Post(repayment));
        Assert.Throws<ArgumentException>(()=>f.Post(Opening(f,9000,"2300") with{LoanId=loan.Id}));
        var opening=f.Post(Opening(f,10000,"2300") with{LoanId=loan.Id});
        f.Post(repayment);
        Assert.Equal(-8000,f.Balance("2300"));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=opening.Id}));
    }

    [Fact]
    public void ApprovedAssetBookOpeningDoesNotInventTaxBasisAndDepreciationStaysBounded()
    {
        using var f=new LedgerFixture();
        var opening=f.Post(Opening(f,30000,"1500") with{AssetId=f.Asset.Id});
        f.Post(f.Draft(FinancialKind.Depreciation,10000));
        Assert.Equal(30000,f.Balance("1500"));
        Assert.Equal(-10000,f.Balance("1590"));
        Assert.Null(new BusinessResources(f.Business.Store).Assets(f.Business.Owner).Single().TaxBasisCents);
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=opening.Id}));
    }

    [Fact]
    public void OpeningFailureRollsBackTheLedgerAndConfirmationBeforeRetry()
    {
        using var f=new LedgerFixture();
        var draft=f.Book.SaveDraft(f.Business.Owner,Opening(f,10000));
        f.Book.FailurePoint=p=>{if(p=="opening-record")throw new IOException("Injected opening failure");};
        Assert.Throws<IOException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Empty(new OpeningBalances(f.Business.Store).List(f.Business.Owner));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
        Assert.False(new ChartOfAccounts(f.Business.Store).List(f.Business.Owner).Single(a=>a.Code=="1010").OpeningConfirmed);
        f.Book.FailurePoint=null;
        f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Single(new OpeningBalances(f.Business.Store).List(f.Business.Owner));
    }

    [Fact]
    public void MissingEvidenceWrongClassificationAndLateOpeningDatesAreRejected()
    {
        using var f=new LedgerFixture();
        Assert.Throws<ArgumentException>(()=>f.Post(Opening(f,10000) with{EvidenceId=""}));
        Assert.Throws<ArgumentException>(()=>f.Post(Opening(f,10000,"4000")));
        Assert.Throws<ArgumentException>(()=>f.Post(Opening(f,10000) with{AssetId=f.Asset.Id}));
        f.Post(f.Draft(FinancialKind.Receipt,12000));
        Assert.Throws<ArgumentException>(()=>f.Post(Opening(f,10000) with{Date=f.Date.AddDays(1)}));
        Assert.Throws<ArgumentException>(()=>new OpeningBalances(f.Business.Store).ConfirmZero(f.Business.Owner,f.Accounts["1010"].Id,
            f.Date.AddDays(1),"Fictional incorrect later opening",BusinessFixture.Phrase));
        Assert.Empty(new OpeningBalances(f.Business.Store).List(f.Business.Owner));
    }
}
