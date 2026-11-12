using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableLedgerReportTablesTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];

    [Fact]
    public void CapturedReportsSeparateIncomeExpensesTaxLoansOwnerCashAndLaterChanges()
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.Contribution,20000));
        f.Post(f.Draft(FinancialKind.LoanProceeds,10000));
        f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000,FeeCents=300});
        f.Post(f.Draft(FinancialKind.Expense,2500));
        f.Post(f.Draft(FinancialKind.TaxRemittance,1000));
        f.Post(f.Draft(FinancialKind.Withdrawal,2000));
        var expected=new FinancialReports(f.Business.Store).Load(f.Business.Owner,f.Date,f.Date);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        f.Post(f.Draft(FinancialKind.Expense,1000));
        f.Business.Owner.Lock();
        var tables=ReadableLedgerReportTables.Build(source,new(),f.Date,f.Date);
        Assert.Equal(6,tables.Length);
        var profit=tables.Single(t=>t.Name=="Report profit and loss");
        var pnl=Assert.Single(profit.Rows);
        Assert.Equal("10000",Cell(profit,pnl,"IncomeCents"));
        Assert.Equal("2800",Cell(profit,pnl,"ExpenseCents"));
        Assert.Equal("7200",Cell(profit,pnl,"ResultCents"));
        var balance=tables.Single(t=>t.Name=="Report balance sheet");
        Assert.Equal("35200",Cell(balance,Assert.Single(balance.Rows),"AssetsCents"));
        Assert.Equal("true",Cell(balance,balance.Rows[0],"Balances"));
        var trial=tables.Single(t=>t.Name=="Report trial balance");
        foreach(var item in expected.TrialBalance)
        {
            var row=trial.Rows.Single(r=>Cell(trial,r,"AccountId")==item.Account.Id);
            Assert.Equal(item.Ending.ToString(),Cell(trial,row,"EndingCents"));
        }
        var cash=tables.Single(t=>t.Name=="Report cash activity");
        Assert.Equal(35200,cash.Rows.Sum(r=>long.Parse(Cell(cash,r,"DebitCents"))-long.Parse(Cell(cash,r,"CreditCents"))));
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
        Assert.Equal(tables.Select(t=>t.Sha256),ReadableLedgerReportTables.Build(source,new(),f.Date,f.Date).Select(t=>t.Sha256));
        Assert.All(tables,t=>Assert.Equal(source.Source.Watermark,t.Watermark));
    }

    [Fact]
    public void ReopenedPeriodKeepsItsOriginalReportsAlongsideNewCapturedReports()
    {
        using var f=new LedgerFixture();
        BookPeriodTests.Ready(f);
        const string evidence="Fictional-private-retained-report-review";
        var periods=new BookPeriods(f.Business.Store);
        var closed=BookPeriodTests.Close(f,periods,new(new(2027,1,1),new(2027,1,31),evidence,evidence));
        periods.Reopen(f.Business.Owner,closed.Id,evidence,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        f.Post(f.Draft(FinancialKind.Expense,500) with{Date=new(2027,2,1)});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableLedgerReportTables.Build(source,new(),new(2027,1,1),new(2027,2,1));
        Assert.DoesNotContain(evidence,JsonSerializer.Serialize(tables));
        var scopes=tables.Single(t=>t.Name=="Ledger report scopes");
        Assert.Equal(2,scopes.Rows.Count);
        var retained=scopes.Rows.Single(r=>r[0]==closed.Id);
        Assert.Equal("RetainedPeriod",Cell(scopes,retained,"SourceKind"));
        Assert.Equal("2027-01-31",Cell(scopes,retained,"Through"));
        Assert.Equal(closed.Snapshot.Reports.Watermark.ToString(),Cell(scopes,retained,"ObservedWatermark"));
        var profit=tables.Single(t=>t.Name=="Report profit and loss");
        Assert.Equal("10000",Cell(profit,profit.Rows.Single(r=>r[0]==closed.Id),"ResultCents"));
        Assert.Equal("9500",Cell(profit,profit.Rows.Single(r=>r[0]==source.Source.GenerationId),"ResultCents"));
        var cash=tables.Single(t=>t.Name=="Report cash activity");
        Assert.Single(cash.Rows,r=>r[0]==closed.Id);
        Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
    }
}
