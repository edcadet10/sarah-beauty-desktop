using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class DebtReportKernelTests
{
    [Fact]
    public void CapturedDebtRollForwardsMatchHumanReportsWithoutLosingOriginalLender()
    {
        using var f=new LedgerFixture();
        var resources=new BusinessResources(f.Business.Store);
        var loan=resources.SaveLoan(f.Business.Owner,f.Loan with{OwnerLenderId=f.Business.Owner.OwnerId});
        f.Post(f.Draft(FinancialKind.LoanProceeds,30000));
        var payment=f.Post(f.Draft(FinancialKind.LoanPayment,11200) with{PrincipalCents=10000,InterestCents=1000,FeeCents=200,Date=new(2027,2,10)});
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=payment.Id,Date=new(2027,3,10)});
        var expected=Enumerable.Range(1,3).Select(month=>new FinancialReports(f.Business.Store).Load(f.Business.Owner,new(2027,month,1),new(2027,month,DateTime.DaysInMonth(2027,month))).Debts!).ToArray();
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        resources.SaveLoan(f.Business.Owner,loan with{Name="Fictional later lender name",Archived=true});f.Business.Owner.Lock();
        for(int month=1;month<=3;month++)
        {
            var actual=source.Read(db=>DebtSchedule.Read(db,source.Source.BusinessId,new(2027,month,1),new(2027,month,DateTime.DaysInMonth(2027,month))));
            Assert.Equal(expected[month-1].Positions,actual.Positions);Assert.Equal(expected[month-1].Entries,actual.Entries);
            Assert.All(actual.Entries,e=>Assert.Equal(loan.Name,e.CapturedLender!.LenderName));
        }
        Assert.Equal(20000,expected[1].Positions.Single().ClosingPrincipalCents);
        Assert.Equal(-10000,expected[2].Positions.Single().PrincipalPaidCents);
        Assert.Equal(30000,expected[2].Positions.Single().ClosingPrincipalCents);
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }

    [Fact]
    public void RegisteredPrincipalDoesNotCreatePostedDebtAndForeignScopeStaysEmpty()
    {
        using var f=new LedgerFixture();using var foreign=new BusinessFixture();
        new BusinessResources(f.Business.Store).SaveLoan(f.Business.Owner,f.Loan with{OpeningDate=f.Date,OpeningPrincipalCents=10000});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);f.Business.Owner.Lock();
        source.Read(db=>
        {
            var row=Assert.Single(DebtSchedule.Read(db,source.Source.BusinessId,f.Date,f.Date).Positions);
            Assert.Equal(10000,row.CurrentFacts.OpeningPrincipalCents);Assert.Null(row.PostedOpeningPrincipalCents);Assert.Null(row.ClosingPrincipalCents);
            var other=DebtSchedule.Read(db,foreign.Owner.BusinessId,f.Date,f.Date);
            Assert.Empty(other.Positions);Assert.Empty(other.Entries);return true;
        });
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }
}
