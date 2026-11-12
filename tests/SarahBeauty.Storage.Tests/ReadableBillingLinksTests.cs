using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableBillingLinksTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];
    [Fact]
    public void InvoicePayersQuotedPortionsAndPartialCreditReductionsRetainTheirExactSourceLinks()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10001,0.075m);var notes=new CreditNoteBook(f.Business.Store);var draft=notes.SaveDraft(f.Business.Owner,new CreditNoteDraft{Name="Fictional linked partial credit",InvoiceVersionId=invoice.Id,IssueDate=f.Date,Portions=[new(invoice.Snapshot.Lines.Single().ChargeId,0.5m)]});var credit=notes.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableBillingLinks.Build(source,new());Assert.Equal(7,tables.Length);var headers=tables.Single(t=>t.Name=="Invoice payers");var payer=Assert.Single(headers.Rows);Assert.Equal(invoice.Id,Cell(headers,payer,"InvoiceVersionId"));Assert.Equal(f.CustomerId,Cell(headers,payer,"CustomerId"));Assert.NotEmpty(Cell(headers,payer,"SourceQuoteVersionId"));var portions=tables.Single(t=>t.Name=="Invoice billed portions");Assert.Equal("10001",Cell(portions,portions.Rows.Single(),"NetCents"));Assert.Equal("750",Cell(portions,portions.Rows.Single(),"TaxCents"));var milestones=tables.Single(t=>t.Name=="Invoice payment milestones");Assert.Equal("",Cell(milestones,milestones.Rows.Single(),"DueDate"));var links=tables.Single(t=>t.Name=="Credit note invoice links");Assert.Equal(invoice.Id,Cell(links,links.Rows.Single(),"InvoiceVersionId"));var reductions=tables.Single(t=>t.Name=="Credit note portions");Assert.Equal("0.5",Cell(reductions,reductions.Rows.Single(),"Quantity"));Assert.Equal(credit.Snapshot.Lines.Single().NetCents.ToString(),Cell(reductions,reductions.Rows.Single(),"NetCents"));var reducedMilestones=tables.Single(t=>t.Name=="Credit note milestone reductions");Assert.Equal(credit.Snapshot.TotalCents.ToString(),Cell(reducedMilestones,reducedMilestones.Rows.Single(),"Cents"));Assert.Equal(watermark,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;"));
    }
    [Fact]
    public void ReceiptVersionRetainsThePostedPaymentIdentityWithoutRecreatingCash()
    {
        using var f=new LedgerFixture();var payment=f.Post(f.Draft(FinancialKind.Receipt,10000));var book=new ReceiptBook(f.Business.Store);var draft=book.SaveDraft(f.Business.Owner,new ReceiptDraft{Name="Fictional linked receipt",PaymentId=payment.Id,IssueDate=f.Date});var receipt=book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var table=ReadableBillingLinks.Build(source,new()).Single(t=>t.Name=="Receipt payment links");Assert.Equal(receipt.Id,table.Rows.Single()[0]);Assert.Equal(payment.Id,table.Rows.Single()[2]);Assert.Equal(1L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Movements;"));
    }
    [Fact]
    public void IncorrectProjectionShapeOrBinaryDataCannotSilentlyBecomeReadableCells()
    {
        using var f=new LedgerFixture();using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);Assert.Throws<InvalidDataException>(()=>source.Read(db=>ReadableSqlTables.Read(db,source.Source,new(),"Wrong shape",["OnlyOne"],"SELECT 1,2 WHERE $business IS NOT NULL;")));Assert.Throws<InvalidDataException>(()=>source.Read(db=>ReadableSqlTables.Read(db,source.Source,new(),"Wrong type",["Binary"],"SELECT zeroblob(1) WHERE $business IS NOT NULL;")));
    }
}
