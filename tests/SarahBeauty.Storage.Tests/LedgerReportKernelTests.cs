using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Exports;

namespace SarahBeauty.Storage.Tests;

public sealed class LedgerReportKernelTests
{
    [Fact]
    public void SnapshotOpeningStatusFollowsOpeningAndReversalDates()
    {
        using var f=new LedgerFixture();
        var opening=f.Post(f.Draft(FinancialKind.Opening,10000) with{CounterAccountId=f.Accounts["1010"].Id,EvidenceId="Fictional dated opening",Date=f.Date.AddDays(1)});
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=opening.Id,Date=f.Date.AddDays(2)});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        bool Confirmed(int days)=>source.Read(db=>LedgerReportKernel.TrialBalance(db,source.Source.BusinessId,f.Date,f.Date.AddDays(days)).Single(r=>r.Account.Code=="1010").Account.OpeningConfirmed);
        Assert.False(Confirmed(0));
        Assert.True(Confirmed(1));
        Assert.False(Confirmed(2));
    }

    [Fact]
    public void CapturedKernelUsesOneGenerationWithoutRenewingLockedHumanAccess()
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.Receipt,10000));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        f.Post(f.Draft(FinancialKind.Expense,2500));
        f.Business.Owner.Lock();
        var rows=source.Read(db=>LedgerReportKernel.TrialBalance(db,source.Source.BusinessId,f.Date,f.Date));
        Assert.Equal(10000,rows.Single(r=>r.Account.Code=="1010").Ending);
        Assert.Equal(10000,LedgerReportKernel.ProfitLoss(rows).IncomeCents);
        Assert.Equal(0,LedgerReportKernel.ProfitLoss(rows).ExpenseCents);
        Assert.True(LedgerReportKernel.BalanceSheet(rows).Balances);
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
        Assert.Throws<UnauthorizedAccessException>(()=>new LedgerReports(f.Business.Store).TrialBalance(f.Business.Owner,f.Date,f.Date));
        Assert.Equal(source.Source.Watermark+2,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;"));
    }

    [Fact]
    public void TrulyUnrepresentablePeriodTotalsFailWithoutTruncationOrChangingPostings()
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.Contribution,long.MaxValue));
        f.Post(f.Draft(FinancialKind.Contribution,long.MaxValue));
        var error=Assert.Throws<ArgumentException>(()=>new LedgerReports(f.Business.Store).TrialBalance(f.Business.Owner,f.Date,f.Date));
        Assert.Contains("supported cent range",error.Message);
        Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
    }

    [Fact]
    public void EndingUsesExactNetEvenWhenOpeningPlusGrossDebitsExceedsInt64()
    {
        var account=new Account("fictional","1010","Fictional limit test","Asset","Bank",true,false);
        var row=new AccountTotal(account,long.MaxValue,long.MaxValue,long.MaxValue);
        Assert.Equal(long.MaxValue,row.Ending);
    }

    [Fact]
    public void CancellingOpeningMovementsAreSummedExactlyBeforeRangeChecks()
    {
        using var f=new LedgerFixture();
        var first=f.Post(f.Draft(FinancialKind.Contribution,long.MaxValue));
        f.Post(f.Draft(FinancialKind.Contribution,long.MaxValue));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=first.Id});
        var row=new LedgerReports(f.Business.Store).TrialBalance(f.Business.Owner,f.Date.AddDays(1),f.Date.AddDays(1)).Single(r=>r.Account.Code=="1010");
        Assert.Equal(long.MaxValue,row.Opening);
        Assert.Equal(long.MaxValue,row.Ending);
    }
}
