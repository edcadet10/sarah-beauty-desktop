using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableBookingBillingTablesTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];
    private static DocumentVersion Reduce(LedgerFixture f,DocumentVersion invoice)
    {
        var book=new CreditNoteBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,new(){Name="Fictional agreed half reduction",InvoiceVersionId=invoice.Id,IssueDate=f.Date,Portions=[new(invoice.Snapshot.Lines.Single().ChargeId,0.5m)]});
        return book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }

    [Fact]
    public void ActualHalfCreditAndApplicationKeepTaxZeroDueFrozenPayerAndSourceLines()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000,0.1m);
        var payment=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000});CancellationSourceTests.Apply(f,payment.Id,invoice,5500);
        var credit=Reduce(f,invoice);
        var payer=f.Business.Store.Read(db=>CustomerBook.Find(db,f.Business.Owner,f.CustomerId)!);
        new CustomerBook(f.Business.Store).Save(f.Business.Owner,payer with{FullName="Fictional later payer name"});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);f.Business.Owner.Lock();
        var tables=ReadableBookingBillingTables.Build(source,new(),new(2027,1,31));Assert.Equal(6,tables.Length);
        var invoices=tables.Single(t=>t.Name=="Report booking invoice balances");var row=Assert.Single(invoices.Rows);
        Assert.Equal(invoice.Id,Cell(invoices,row,"InvoiceVersionId"));Assert.Equal(invoice.Hash,Cell(invoices,row,"SourceSha256"));
        Assert.Equal("5000",Cell(invoices,row,"ReducedNetCents"));Assert.Equal("500",Cell(invoices,row,"ReducedTaxCents"));
        Assert.Equal("5500",Cell(invoices,row,"AppliedCents"));Assert.Equal("500",Cell(invoices,row,"AppliedTaxCents"));
        Assert.Equal("0",Cell(invoices,row,"OpenCents"));Assert.Equal("Fictional payer",Cell(invoices,row,"FrozenPayerName"));
        var credits=tables.Single(t=>t.Name=="Report booking invoice credits");Assert.Equal(credit.Id,Cell(credits,Assert.Single(credits.Rows),"CreditNoteVersionId"));
        var lines=tables.Single(t=>t.Name=="Report booking billed lines");Assert.Equal(2,lines.Rows.Count);
        Assert.Equal("0.5",Cell(lines,lines.Rows.Single(r=>Cell(lines,r,"Kind")=="CreditNote"),"Quantity"));
        var dues=tables.Single(t=>t.Name=="Report booking milestone dues");Assert.NotEmpty(dues.Rows);Assert.All(dues.Rows,r=>Assert.Equal("0",Cell(dues,r,"Cents")));
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }

    [Fact]
    public void RetainedBalancesAndCreditHistoryStayOriginalAfterReopeningAndNewReduction()
    {
        using var f=new LedgerFixture();BookPeriodTests.Ready(f);var invoice=ApplicationTests.Invoice(f,5000);
        var periods=new BookPeriods(f.Business.Store);var closed=BookPeriodTests.Close(f,periods);
        periods.Reopen(f.Business.Owner,closed.Id,"Fictional later billing review",Guid.NewGuid().ToString(),BusinessFixture.Phrase);Reduce(f,invoice);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableBookingBillingTables.Build(source,new(),new(2027,1,31));
        var invoices=tables.Single(t=>t.Name=="Report booking invoice balances");
        Assert.Equal("5000",Cell(invoices,invoices.Rows.Single(r=>r[0]==closed.Id),"OpenCents"));
        Assert.Equal("2500",Cell(invoices,invoices.Rows.Single(r=>r[0]==source.Source.GenerationId),"OpenCents"));
        var credits=tables.Single(t=>t.Name=="Report booking invoice credits");Assert.Single(credits.Rows);Assert.DoesNotContain(credits.Rows,r=>r[0]==closed.Id);
        var scopes=tables.Single(t=>t.Name=="Booking billing report scopes");
        Assert.Equal(closed.Snapshot.Reports.Watermark.ToString(),Cell(scopes,scopes.Rows.Single(r=>r[0]==closed.Id),"ObservedWatermark"));
        Assert.Single(f.Book.Posted(f.Business.Owner));
    }
}
