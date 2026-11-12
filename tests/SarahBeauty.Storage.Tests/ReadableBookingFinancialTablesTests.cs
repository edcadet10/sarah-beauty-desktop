using System.Text.Json;
using System.Text.Json.Nodes;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableBookingFinancialTablesTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];
    private const string Private="Fictional-private-booking-financial-note";

    [Fact]
    public void BilledContributionRemainsDistinctFromTaggedCashServiceIncomeAndUnassignedCosts()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000);
        var book=new BookingBook(f.Business.Store);book.Save(f.Business.Owner,book.List(f.Business.Owner).Single() with{PrivateNotes=Private});
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,5000) with{BookingId=invoice.BookingId,FeeCents=100});
        CancellationSourceTests.Apply(f,receipt.Id,invoice,3000);
        f.Post(f.Draft(FinancialKind.Expense,2000) with{BookingId=invoice.BookingId});f.Post(f.Draft(FinancialKind.Expense,900));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);f.Business.Owner.Lock();
        var tables=ReadableBookingFinancialTables.Build(source,new(),new(2027,1,1),new(2027,1,31));
        Assert.Equal(5,tables.Length);Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
        var summary=tables.Single(t=>t.Name=="Report booking contribution");var row=Assert.Single(summary.Rows);
        Assert.Equal("7900",Cell(summary,row,"ContributionEstimateCents"));Assert.Equal("10000",Cell(summary,row,"EffectiveNetChargesCents"));
        Assert.Equal("3000",Cell(summary,row,"AppliedCents"));Assert.Equal("7000",Cell(summary,row,"OpenCents"));
        Assert.Equal("5000",Cell(summary,row,"RecognizedServiceIncomeCents"));Assert.Equal("2100",Cell(summary,row,"RecordedDirectCostCents"));
        Assert.Equal(source.Source.CapturedAtUtc,Cell(summary,row,"FinancialGeneratedAtUtc"));
        var cash=tables.Single(t=>t.Name=="Report booking tagged cash");Assert.Equal("4900",Cell(cash,Assert.Single(cash.Rows),"SignedNetCashCents"));
        var costs=tables.Single(t=>t.Name=="Report unassigned direct costs");var unassigned=Assert.Single(costs.Rows);
        Assert.Equal("",Cell(costs,unassigned,"BookingId"));Assert.Equal("900",Cell(costs,unassigned,"DebitCents"));
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }

    [Fact]
    public void RetainedUnknownEstimateStaysBlankAfterLaterAssignedExpense()
    {
        using var f=new LedgerFixture();BookPeriodTests.Ready(f);
        var bookings=new BookingBook(f.Business.Store);
        var booking=bookings.Save(f.Business.Owner,BookingRules.Wedding("Fictional retained unissued booking",1) with{PrimaryCustomerId=f.CustomerId,PrivateNotes=Private});
        var periods=new BookPeriods(f.Business.Store);var closed=BookPeriodTests.Close(f,periods);
        periods.Reopen(f.Business.Owner,closed.Id,Private,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        f.Post(f.Draft(FinancialKind.OwnerPaidExpense,1000) with{BookingId=booking.Id,Date=new(2027,2,1),EvidenceId=Private});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableBookingFinancialTables.Build(source,new(),new(2027,1,1),new(2027,2,28));
        Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
        var summary=tables.Single(t=>t.Name=="Report booking contribution");
        Assert.All(summary.Rows,r=>Assert.Equal("",Cell(summary,r,"ContributionEstimateCents")));
        Assert.Equal("0",Cell(summary,summary.Rows.Single(r=>r[0]==closed.Id),"RecordedDirectCostCents"));
        Assert.Equal("1000",Cell(summary,summary.Rows.Single(r=>r[0]==source.Source.GenerationId),"RecordedDirectCostCents"));
        Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
    }

    [Theory]
    [InlineData("booking")]
    [InlineData("watermark")]
    [InlineData("date")]
    public void HashValidRetainedReportsWithMixedBookingScopesAreRejected(string defect)
    {
        using var f=new LedgerFixture();BookPeriodTests.Ready(f);
        new BookingBook(f.Business.Store).Save(f.Business.Owner,BookingRules.Wedding("Fictional retained scope check",1) with{PrimaryCustomerId=f.CustomerId});
        BookPeriodTests.Close(f,new(f.Business.Store));
        var node=JsonNode.Parse((string)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT SnapshotJson FROM BookPeriodCloses;")!)!;
        var finance=node["Reports"]!["Bookings"]!["Positions"]![0]!["Financial"]!;
        if(defect=="booking")finance["BookingId"]=Guid.NewGuid().ToString();
        else if(defect=="watermark")finance["Watermark"]=finance["Watermark"]!.GetValue<long>()+1;
        else finance["Through"]="2027-02-01";
        string json=node.ToJsonString();
        f.Business.Store.Transaction(db=>
        {
            db.Execute("DROP TRIGGER BookPeriodCloses_NoUpdate;");
            return db.Execute("UPDATE BookPeriodCloses SET SnapshotJson=$json,SnapshotHash=$hash;",("$json",json),("$hash",BookPeriodQueries.Hash(json)));
        });
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        Assert.Throws<InvalidDataException>(()=>ReadableBookingFinancialTables.Build(source,new(),new(2027,1,1),new(2027,1,31)));
    }
}
