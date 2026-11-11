using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReportTests
{
    [Fact]
    public void AgingSeparatesOverdueTodayFutureAndUnknownDatesOnce()
    {
        using var f=new LedgerFixture();
        var asof=f.Date.AddDays(10);
        ApplicationTests.Invoice(f,1000,dueDate:asof.AddDays(-1));
        ApplicationTests.Invoice(f,2000,dueDate:asof);
        ApplicationTests.Invoice(f,3000,dueDate:asof.AddDays(1));
        ApplicationTests.Invoice(f,4000);
        var report=new FinancialReports(f.Business.Store).Load(f.Business.Owner,f.Date,asof);
        var customer=Assert.Single(report.Customers);
        Assert.Equal(10000,customer.OpenCents);
        Assert.Equal(1000,customer.OverdueCents);
        Assert.Equal(2000,customer.DueTodayCents);
        Assert.Equal(3000,customer.FutureCents);
        Assert.Equal(4000,customer.UnknownDueCents);
        Assert.Equal(0,report.ProfitLoss.IncomeCents);
        Assert.True(report.BalanceSheet.Balances);
    }

    [Fact]
    public void ChargedCollectedRefundedAndRemittedTaxUseTheirOwnDates()
    {
        using var f=new LedgerFixture();
        var invoice=ApplicationTests.Invoice(f,10000,0.1m);
        var payment=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000,Date=f.Date.AddMonths(1)});
        f.Post(f.Draft(FinancialKind.Refund,5500) with{OriginalId=payment.Id,TaxCents=500,Date=f.Date.AddMonths(2)});
        f.Post(f.Draft(FinancialKind.TaxRemittance,500) with{Date=f.Date.AddMonths(3)});
        var reports=new FinancialReports(f.Business.Store);
        var january=reports.Load(f.Business.Owner,new(2027,1,1),new(2027,1,31));
        Assert.Equal(new TaxPosition(1000,0,0,0,0,0),january.Tax);
        Assert.Equal(11000,Assert.Single(january.Customers).OpenCents);
        var february=reports.Load(f.Business.Owner,new(2027,2,1),new(2027,2,28));
        Assert.Equal(new TaxPosition(0,0,1000,0,0,1000),february.Tax);
        Assert.Equal(10000,february.ProfitLoss.IncomeCents);
        var march=reports.Load(f.Business.Owner,new(2027,3,1),new(2027,3,31));
        Assert.Equal(new TaxPosition(0,0,0,500,0,500),march.Tax);
        var april=reports.Load(f.Business.Owner,new(2027,4,1),new(2027,4,30));
        Assert.Equal(new TaxPosition(0,0,0,0,500,0),april.Tax);
    }

    [Fact]
    public void ApprovalDuringReportGenerationCannotMixDatabaseGenerations()
    {
        using var f=new LedgerFixture();
        f.Business.Files.Execute("PRAGMA journal_mode=WAL;");
        f.Post(f.Draft(FinancialKind.Receipt,10000));
        var reports=new FinancialReports(f.Business.Store){AfterTrialRead=()=>f.Post(f.Draft(FinancialKind.Receipt,20000))};
        var snapshot=reports.Load(f.Business.Owner,f.Date,f.Date);
        Assert.Equal(10000,snapshot.ProfitLoss.IncomeCents);
        Assert.Equal(10000,snapshot.Cash.Sum(c=>c.DebitCents-c.CreditCents));
        Assert.Equal(10000,snapshot.Entries.Where(e=>e.AccountId==f.Accounts["4000"].Id).Sum(e=>e.CreditCents-e.DebitCents));
        Assert.Equal(10000,Assert.Single(snapshot.Customers).UnallocatedReceiptsCents);
        Assert.Equal(-30000,f.Balance("4000"));
    }

    [Fact]
    public void FutureOpeningAndReversalDecisionsDoNotChangeEarlierOpeningWarnings()
    {
        using var f=new LedgerFixture();
        var source=f.Post(f.Draft(FinancialKind.Opening,10000) with{CounterAccountId=f.Accounts["1010"].Id,EvidenceId="Fictional opening evidence",Date=f.Date.AddDays(1)});
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=source.Id,Date=f.Date.AddDays(2)});
        var reports=new FinancialReports(f.Business.Store);
        Assert.False(reports.Load(f.Business.Owner,f.Date,f.Date).TrialBalance.Single(r=>r.Account.Code=="1010").Account.OpeningConfirmed);
        Assert.True(reports.Load(f.Business.Owner,f.Date,f.Date.AddDays(1)).TrialBalance.Single(r=>r.Account.Code=="1010").Account.OpeningConfirmed);
        Assert.False(reports.Load(f.Business.Owner,f.Date,f.Date.AddDays(2)).TrialBalance.Single(r=>r.Account.Code=="1010").Account.OpeningConfirmed);
    }
}
