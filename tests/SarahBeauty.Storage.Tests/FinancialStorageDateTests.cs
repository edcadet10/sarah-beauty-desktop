using System.Globalization;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class FinancialStorageDateTests
{
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void NewlyApprovedMovementsKeepGregorianStorageDatesAndCanBeReadAfterCultureChanges(string culture)
    {
        using var f=new LedgerFixture();string id="";WithCulture(culture,()=>{Assert.NotEqual("2027-01-10",f.Date.ToString("yyyy-MM-dd"));id=f.Post(f.Draft(FinancialKind.Expense,1234)).Id;});Assert.Equal("2027-01-10",f.Business.Store.Read(db=>(string)db.Scalar("SELECT Date FROM Movements WHERE Id=$id;",("$id",id))!));var reopened=new BusinessStore(f.Business.Store.DatabasePath);Assert.Equal(1234,new LedgerReports(reopened).ProfitLoss(f.Business.Owner,f.Date,f.Date).ExpenseCents);
    }
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void AStoredClosedPeriodCannotBeBypassedByChangingTheCurrentCalendar(string culture)
    {
        using var f=new LedgerFixture();BookPeriodTests.Ready(f);BookPeriodTests.Close(f,new BookPeriods(f.Business.Store));var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Expense,1234));long watermark=Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));
        WithCulture(culture,()=>{Assert.Contains("closed period",Assert.Throws<ArgumentException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase)).Message,StringComparison.Ordinal);Assert.Contains("closed period",Assert.Throws<ArgumentException>(()=>f.Book.Preview(f.Business.Owner,draft)).Message,StringComparison.Ordinal);});Assert.Equal(watermark,TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Single(f.Book.Posted(f.Business.Owner));
    }
    private static void WithCulture(string name,Action action)
    {
        var previous=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(name);action();}finally{CultureInfo.CurrentCulture=previous;}
    }
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void AnExistingEvidencedLoanOpeningStillAppliesAfterChangingCalendars(string culture)
    {
        using var f=new LedgerFixture();var loan=new BusinessResources(f.Business.Store).SaveLoan(f.Business.Owner,new(){Name="Fictional dated opening loan",OpeningDate=f.Date,OpeningPrincipalCents=10000});f.Post(f.Draft(FinancialKind.Opening,10000) with{CounterAccountId=f.Accounts["2300"].Id,LoanId=loan.Id,OpeningCredit=true,EvidenceId="Fictional opening evidence"});WithCulture(culture,()=>f.Post(f.Draft(FinancialKind.LoanPayment,2000) with{LoanId=loan.Id,PrincipalCents=2000}));Assert.Equal(-8000,f.Balance("2300"));
    }
}
