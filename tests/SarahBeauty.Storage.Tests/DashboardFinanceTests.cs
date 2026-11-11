using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Dashboard;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class DashboardFinanceTests
{
    private static DashboardFinanceSnapshot Read(LedgerFixture f,DateOnly? through=null)=>new DashboardFinanceSources(f.Business.Store).Load(f.Business.Owner,new(2027,1,1),through??new(2027,3,31));

    [Fact]
    public void MonthlyResultExpensesAndCashReconcileWithoutTurningFundingIntoIncome()
    {
        using var f=new LedgerFixture();
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,10000) with{FeeCents=300});
        f.Post(f.Draft(FinancialKind.Contribution,5000));f.Post(f.Draft(FinancialKind.LoanProceeds,10000));
        f.Post(f.Draft(FinancialKind.AssetPurchase,3000));f.Post(f.Draft(FinancialKind.Expense,2000));f.Post(f.Draft(FinancialKind.Depreciation,500));
        f.Post(f.Draft(FinancialKind.LoanPayment,1200) with{PrincipalCents=1000,InterestCents=150,FeeCents=50});
        f.Post(f.Draft(FinancialKind.Refund,2000) with{OriginalId=receipt.Id,Date=new(2027,2,10)});
        f.Post(f.Draft(FinancialKind.AssetDisposal,4000) with{AssetId=f.Asset.Id,Date=new(2027,2,10),EvidenceId="Fictional disposal decision"});
        var snapshot=Read(f);var january=snapshot.Months[0];var february=snapshot.Months[1];var march=snapshot.Months[2];
        Assert.Equal(10000,january.IncomeCents);Assert.Equal(3000,january.ExpenseCents);Assert.Equal(7000,january.ResultCents);
        Assert.Equal(-500,february.IncomeCents);Assert.Equal(-500,february.ResultCents);Assert.Null(march.IncomeCents);Assert.False(march.HasPostedActivity);
        Assert.Equal(snapshot.Reports.ProfitLoss.IncomeCents,snapshot.Months.Sum(m=>m.IncomeCents));Assert.Equal(6500,snapshot.Reports.ProfitLoss.ResultCents);
        Assert.Equal(3000,snapshot.Expenses.Sum(e=>e.Cents));Assert.Equal(500,snapshot.Expenses.Single(e=>e.Code=="5040").Cents);Assert.DoesNotContain(snapshot.Expenses,e=>e.Code=="2300");
        var bank=snapshot.CashAccounts.Single(c=>c.Account.Code=="1010");Assert.Equal(20500,bank.BalanceCents);Assert.False(bank.Account.OpeningConfirmed);
        Assert.Equal(20500,snapshot.CashEntries.Where(e=>e.AccountId==bank.Account.Id).Sum(e=>e.DebitCents-e.CreditCents));
    }

    [Fact]
    public void AgingPlacesTodayAndFutureTogetherAndCountsEachRemainingShareOnce()
    {
        using var f=new LedgerFixture();DateOnly asOf=new(2027,2,10);
        var today=ApplicationTests.Invoice(f,1000,dueDate:asOf);ApplicationTests.Invoice(f,2000,dueDate:asOf.AddDays(1));
        ApplicationTests.Invoice(f,3000,dueDate:asOf.AddDays(-1));ApplicationTests.Invoice(f,4000,dueDate:asOf.AddDays(-7));
        ApplicationTests.Invoice(f,5000,dueDate:asOf.AddDays(-8));ApplicationTests.Invoice(f,6000,dueDate:asOf.AddDays(-30));
        ApplicationTests.Invoice(f,7000,dueDate:asOf.AddDays(-31));ApplicationTests.Invoice(f,8000);
        var received=f.Post(f.Draft(FinancialKind.Receipt,400));var apps=new PaymentApplications(f.Business.Store);
        var draft=apps.SaveDraft(f.Business.Owner,new PaymentApplicationDraft{Name="Fictional aging partial payment",InvoiceVersionId=today.Id,PaymentId=received.Id,Cents=400,Date=asOf});
        apps.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        var snapshot=Read(f,asOf);Assert.Equal(new long[]{2600,7000,11000,7000,8000},snapshot.Aging.Select(b=>b.Cents));
        Assert.Equal(35600,snapshot.Aging.Sum(b=>b.Cents));Assert.Equal(8,snapshot.Aging.Sum(b=>b.Sources.Length));
        Assert.Equal(snapshot.Reports.Customers.Sum(c=>c.OpenCents),snapshot.Aging.Sum(b=>b.Cents));
    }

    [Fact]
    public void ClosedNoActivityMonthHasKnownZeroUntilReopenedAndReconciliationStaysSeparate()
    {
        using var f=new LedgerFixture();BookPeriodTests.Ready(f);var periods=new BookPeriods(f.Business.Store);
        var january=BookPeriodTests.Close(f,periods);BookPeriodTests.ReconcileMonth(f,2);
        var february=BookPeriodTests.Close(f,periods,new(new(2027,2,1),new(2027,2,28),"Fictional no-activity close","Fictional February evidence"));
        var closed=Read(f);Assert.Equal(0,closed.Months[1].ResultCents);Assert.True(closed.Months[1].HasClosedCoverage);Assert.Null(closed.Months[2].ResultCents);
        Assert.All(closed.CashAccounts,c=>Assert.True(c.Account.OpeningConfirmed));var bank=closed.CashAccounts.Single(c=>c.Account.Code=="1010");
        Assert.Equal(new DateOnly(2027,2,28),bank.ReconciledThrough);Assert.Equal(10000,bank.BalanceCents);Assert.NotEmpty(bank.ReconciliationHash);
        Assert.Null(Read(f,new(2027,1,30)).CashAccounts.Single(c=>c.Account.Code=="1010").ReconciledThrough);
        periods.Reopen(f.Business.Owner,february.Id,"Fictional reopened month",Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        var reopened=Read(f);Assert.Null(reopened.Months[1].ResultCents);Assert.Equal(new DateOnly(2027,2,28),reopened.CashAccounts.Single(c=>c.Account.Code=="1010").ReconciledThrough);
        new Reconciliations(f.Business.Store).Reopen(f.Business.Owner,bank.ReconciliationId,"Fictional separate bank review",Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        Assert.Equal(new DateOnly(2027,1,31),Read(f).CashAccounts.Single(c=>c.Account.Code=="1010").ReconciledThrough);
        f.Business.Files.Execute("DROP TRIGGER BookPeriodCloses_NoUpdate;");
        f.Business.Store.Transaction(db=>db.Execute("UPDATE BookPeriodCloses SET SnapshotHash=$hash WHERE Id=$id;",("$hash",new string('0',64)),("$id",january.Id)));
        Assert.Contains("closed report",Assert.Throws<InvalidDataException>(()=>Read(f)).Message);
    }

    [Fact]
    public void ConcurrentPostingCannotMixReportChartsCashSourcesOrWatermark()
    {
        using var f=new LedgerFixture();f.Business.Files.Execute("PRAGMA journal_mode=WAL;");f.Post(f.Draft(FinancialKind.Receipt,10000));
        long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        var sources=new DashboardFinanceSources(f.Business.Store){AfterReportsRead=()=>f.Post(f.Draft(FinancialKind.Expense,2000) with{Date=new(2027,2,10)})};
        var snapshot=sources.Load(f.Business.Owner,new(2027,1,1),new(2027,3,31));
        Assert.Equal(watermark,snapshot.Reports.Watermark);Assert.Equal(0,snapshot.Expenses.Sum(e=>e.Cents));Assert.Null(snapshot.Months[1].ExpenseCents);
        Assert.Equal(10000,snapshot.CashAccounts.Single(c=>c.Account.Code=="1010").BalanceCents);Assert.Equal(10000,snapshot.CashEntries.Sum(e=>e.DebitCents-e.CreditCents));
        var current=Read(f);Assert.Equal(2000,current.Months[1].ExpenseCents);Assert.Equal(8000,current.CashAccounts.Single(c=>c.Account.Code=="1010").BalanceCents);
    }

    [Fact]
    public void WindowLimitsPreserveBoundaryDatesAndDoNotInventEmptyMonthValues()
    {
        using var f=new LedgerFixture();var sources=new DashboardFinanceSources(f.Business.Store);
        var decade=sources.Load(f.Business.Owner,new(2020,1,1),new(2029,12,31));Assert.Equal(120,decade.Months.Length);Assert.All(decade.Months,m=>Assert.Null(m.ResultCents));
        Assert.Throws<ArgumentException>(()=>sources.Load(f.Business.Owner,new(2020,1,1),new(2030,1,1)));
        Assert.Throws<ArgumentException>(()=>sources.Load(f.Business.Owner,new(2027,2,1),new(2027,1,1)));
        var last=sources.Load(f.Business.Owner,new(9999,12,1),DateOnly.MaxValue);Assert.Equal(new DateOnly(9999,12,1),Assert.Single(last.Months).Month);
    }

    [Fact]
    public void CashCardRejectsAReconciliationWithAnInvalidSnapshotHash()
    {
        using var f=new LedgerFixture();BookPeriodTests.Ready(f);var before=Read(f);string id=before.CashAccounts.Single(c=>c.Account.Code=="1010").ReconciliationId;
        Assert.NotEmpty(id);f.Business.Files.Execute("DROP TRIGGER Reconciliations_NoUpdate;");
        f.Business.Store.Transaction(db=>db.Execute("UPDATE Reconciliations SET SnapshotHash=$hash WHERE Id=$id;",("$hash",new string('0',64)),("$id",id)));
        Assert.Throws<InvalidDataException>(()=>Read(f));
    }
}
