using System.Numerics;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ExactBalanceGuardTests
{
    [Theory]
    [InlineData(FinancialKind.LoanProceeds,false)][InlineData(FinancialKind.LoanProceeds,true)]
    [InlineData(FinancialKind.OwnerPaidExpense,false)][InlineData(FinancialKind.OwnerPaidExpense,true)]
    [InlineData(FinancialKind.AssetPurchase,false)][InlineData(FinancialKind.AssetPurchase,true)]
    public void LargeNonnegativeHistoriesKeepExactDailyAndRunningBalances(FinancialKind kind,bool nextDay)
    {
        using var f=new LedgerFixture();f.Post(f.Draft(kind,long.MaxValue));f.Post(f.Draft(kind,long.MaxValue) with{Date=nextDay?f.Date.AddDays(1):f.Date});var values=f.Book.Posted(f.Business.Owner);Assert.Equal(2,values.Count);
        foreach(var movement in values){var lines=f.Book.Journal(f.Business.Owner,movement.Id);Assert.Equal(new BigInteger(long.MaxValue),lines.Aggregate(BigInteger.Zero,(total,line)=>total+line.DebitCents));Assert.Equal(new BigInteger(long.MaxValue),lines.Aggregate(BigInteger.Zero,(total,line)=>total+line.CreditCents));}
        var saved=f.Book.SaveDraft(f.Business.Owner,f.Draft(kind==FinancialKind.LoanProceeds?FinancialKind.LoanPayment:kind==FinancialKind.OwnerPaidExpense?FinancialKind.OwnerReimbursement:FinancialKind.Depreciation,1) with{PrincipalCents=kind==FinancialKind.LoanProceeds?1:0,Date=f.Date.AddDays(-1)});
        long watermark=Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Throws<ArgumentException>(()=>f.Book.Approve(f.Business.Owner,saved.Id,saved.Revision,BusinessFixture.Phrase));Assert.Equal(watermark,TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
    }
    [Fact]
    public void CreditNormalBalanceDoesNotWrapAtTheSignedMinimum()
    {
        using var f=new LedgerFixture();f.Post(f.Draft(FinancialKind.LoanProceeds,long.MaxValue));f.Post(f.Draft(FinancialKind.LoanProceeds,1));Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void LargeTaxLiabilityStillAllowsAValidRemittanceAndRejectsAnEarlierOne(bool nextDay)
    {
        using var f=new LedgerFixture();f.Post(f.Draft(FinancialKind.Receipt,long.MaxValue) with{TaxCents=long.MaxValue});f.Post(f.Draft(FinancialKind.Receipt,long.MaxValue) with{TaxCents=long.MaxValue,Date=nextDay?f.Date.AddDays(1):f.Date});f.Post(f.Draft(FinancialKind.TaxRemittance,1) with{Date=f.Date.AddDays(1)});var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.TaxRemittance,1) with{Date=f.Date.AddDays(-1)});long watermark=Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Throws<ArgumentException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));Assert.Equal(watermark,TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Equal(3,f.Book.Posted(f.Business.Owner).Count);
    }
}
