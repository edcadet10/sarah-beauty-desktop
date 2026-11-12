using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableBookHistoryTests
{
    private const string Private="Fictional-private-opening-close-review-evidence";
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];

    [Fact]
    public void ConfirmedZeroRevocationAndNonzeroOpeningReversalKeepSeparateHistory()
    {
        using var f=new LedgerFixture();
        var book=new OpeningBalances(f.Business.Store);
        book.ConfirmZero(f.Business.Owner,f.Accounts["1010"].Id,f.Date,Private,BusinessFixture.Phrase);
        var zero=Assert.Single(book.List(f.Business.Owner));
        book.RevokeZero(f.Business.Owner,zero.Id,Private,BusinessFixture.Phrase);
        var opening=f.Post(f.Draft(FinancialKind.Opening,10000) with{CounterAccountId=f.Accounts["1010"].Id,EvidenceId=Private});
        var reversal=f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=opening.Id,Date=f.Date.AddDays(1)});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableBookHistory.Build(source,new());
        Assert.Equal(4,tables.Length);
        Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
        var history=tables.Single(t=>t.Name=="Opening balance history");
        Assert.Equal(2,history.Rows.Count);
        var confirmed=history.Rows.Single(r=>r[0]==zero.Id);
        Assert.Equal("0",Cell(history,confirmed,"Cents"));
        Assert.Equal("",Cell(history,confirmed,"MovementId"));
        Assert.NotEmpty(Cell(history,confirmed,"RevokedAtUtc"));
        var posted=history.Rows.Single(r=>Cell(history,r,"MovementId")==opening.Id);
        Assert.Equal(reversal.Id,Cell(history,posted,"ReversalMovementId"));
        Assert.Equal(0,f.Balance("1010"));
        Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
    }

    [Fact]
    public void ReopenedPeriodRetainsExactReportObservationAndReconciliationDependencies()
    {
        using var f=new LedgerFixture();
        BookPeriodTests.Ready(f);
        var book=new BookPeriods(f.Business.Store);
        var saved=BookPeriodTests.Close(f,book,new(new(2027,1,1),new(2027,1,31),Private,Private));
        book.Reopen(f.Business.Owner,saved.Id,Private,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        f.Post(f.Draft(FinancialKind.Expense,500) with{Date=new(2027,2,1)});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableBookHistory.Build(source,new());
        Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
        var closes=tables.Single(t=>t.Name=="Book period close history");
        var row=Assert.Single(closes.Rows);
        Assert.Equal(saved.SnapshotHash,Cell(closes,row,"SnapshotSha256"));
        Assert.Equal(saved.Snapshot.Reports.Watermark.ToString(),Cell(closes,row,"ObservedWatermark"));
        Assert.Equal("true",Cell(closes,row,"Reopened"));
        var dependencies=tables.Single(t=>t.Name=="Closed period reconciliations");
        Assert.Equal(3,dependencies.Rows.Count);
        Assert.All(dependencies.Rows,r=>Assert.Equal(saved.Id,r[0]));
        Assert.Single(tables.Single(t=>t.Name=="Book period reopen history").Rows);
        Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
    }
}
