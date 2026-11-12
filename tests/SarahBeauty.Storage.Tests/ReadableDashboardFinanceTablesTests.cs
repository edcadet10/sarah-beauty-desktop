using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableDashboardFinanceTablesTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];

    [Fact]
    public void ChartValuesKeepUnknownMonthsExactAgingAndActualOwnerMovementsAfterLaterChanges()
    {
        using var f=new LedgerFixture();ApplicationTests.Invoice(f,5000,dueDate:new(2027,3,31));
        f.Post(f.Draft(FinancialKind.Receipt,10000));f.Post(f.Draft(FinancialKind.Expense,2500));f.Post(f.Draft(FinancialKind.Contribution,10000));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        f.Post(f.Draft(FinancialKind.Expense,500) with{Date=new(2027,3,10)});f.Business.Owner.Lock();
        var tables=ReadableDashboardFinanceTables.Build(source,new(),new(2027,1,1),new(2027,3,31));Assert.Equal(8,tables.Length);
        var months=tables.Single(t=>t.Name=="Dashboard monthly financial data");Assert.Equal(3,months.Rows.Count);
        Assert.Equal("7500",Cell(months,months.Rows[0],"ResultCents"));Assert.Equal("",Cell(months,months.Rows[2],"IncomeCents"));Assert.Equal("false",Cell(months,months.Rows[2],"HasPostedActivity"));
        var bands=tables.Single(t=>t.Name=="Dashboard customer aging bands");Assert.Equal(5000,bands.Rows.Sum(r=>long.Parse(Cell(bands,r,"Cents"))));
        var dues=tables.Single(t=>t.Name=="Dashboard aging milestone sources");Assert.Equal(5000,dues.Rows.Sum(r=>long.Parse(Cell(dues,r,"Cents"))));
        var owners=tables.Single(t=>t.Name=="Dashboard owner movement data");var owner=owners.Rows.Single(r=>Cell(owners,r,"OwnerId")==f.Business.Owner.OwnerId);Assert.Equal("10000",Cell(owners,owner,"ContributionsCents"));
        var cash=tables.Single(t=>t.Name=="Dashboard cash account balances");Assert.Equal("17500",Cell(cash,cash.Rows.Single(r=>Cell(cash,r,"Code")=="1010"),"BalanceCents"));
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
        Assert.Equal(tables.Select(t=>t.Sha256),ReadableDashboardFinanceTables.Build(source,new(),new(2027,1,1),new(2027,3,31)).Select(t=>t.Sha256));
    }

    [Fact]
    public void CapturedClosedCoverageAndReconciliationStayStableAndExcludePrivateCloseReview()
    {
        using var f=new LedgerFixture();BookPeriodTests.Ready(f);const string secret="Fictional-private-dashboard-close-evidence";
        var periods=new BookPeriods(f.Business.Store);var closed=BookPeriodTests.Close(f,periods,new(new(2027,1,1),new(2027,1,31),secret,secret));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        periods.Reopen(f.Business.Owner,closed.Id,secret,Guid.NewGuid().ToString(),BusinessFixture.Phrase);f.Business.Owner.Lock();
        var tables=ReadableDashboardFinanceTables.Build(source,new(),new(2027,1,1),new(2027,2,28));Assert.DoesNotContain(secret,JsonSerializer.Serialize(tables));
        var months=tables.Single(t=>t.Name=="Dashboard monthly financial data");Assert.Equal("true",Cell(months,months.Rows[0],"HasClosedCoverage"));Assert.Equal("0",Cell(months,months.Rows[0],"ExpenseCents"));Assert.Equal("",Cell(months,months.Rows[1],"ExpenseCents"));
        var cash=tables.Single(t=>t.Name=="Dashboard cash account balances");var bank=cash.Rows.Single(r=>Cell(cash,r,"Code")=="1010");
        Assert.Equal("2027-01-31",Cell(cash,bank,"ReconciledThrough"));Assert.Equal(64,Cell(cash,bank,"ReconciliationSha256").Length);Assert.Equal("true",Cell(cash,bank,"OpeningConfirmedAsOf"));
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }
}
