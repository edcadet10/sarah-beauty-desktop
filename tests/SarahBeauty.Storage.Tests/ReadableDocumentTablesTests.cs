using System.Text.Json;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableDocumentTablesTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];
    [Fact]
    public void IssuedClientFactsMoneyAndRecordedPdfReferencesStayFrozenWhilePrivateSourceFieldsStayOut()
    {
        using var f=new LedgerFixture();const string secret="Fictional-private-document-sentinel";var invoice=ApplicationTests.Invoice(f,10001,0.075m);new DocumentArtifacts(f.Business.Store).Render(f.Business.Owner,invoice.Id);f.Business.Store.Transaction(db=>db.Execute("UPDATE DocumentArtifacts SET Error=$secret WHERE VersionId=$id;",("$secret",secret),("$id",invoice.Id)));var customers=new CustomerBook(f.Business.Store);var customer=customers.Search(f.Business.Owner).Single();customers.Save(f.Business.Owner,customer with{FullName="Fictional later customer name",PrivateNotes=secret,Sensitivities=secret});
        var payment=f.Post(f.Draft(FinancialKind.Receipt,12000) with{TaxCents=750,FeeCents=360,Name=secret,ExternalReference=secret,EvidenceId=secret,PaymentMethod="Card"});var applications=new PaymentApplications(f.Business.Store);var allocation=applications.SaveDraft(f.Business.Owner,new PaymentApplicationDraft{Name="Fictional document application",PaymentId=payment.Id,InvoiceVersionId=invoice.Id,Cents=8000,Date=f.Date});applications.Approve(f.Business.Owner,allocation.Id,allocation.Revision,BusinessFixture.Phrase);var receipts=new ReceiptBook(f.Business.Store);var draft=receipts.SaveDraft(f.Business.Owner,new ReceiptDraft{Name="Fictional readable receipt",PaymentId=payment.Id,IssueDate=f.Date,ClientReference="Fictional client-safe reference"});var receipt=receipts.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableDocumentTables.Build(source,new());Assert.Equal(5,tables.Length);Assert.DoesNotContain(secret,JsonSerializer.Serialize(tables));var versions=tables.Single(t=>t.Name=="Document versions");var row=versions.Rows.Single(r=>r[0]==invoice.Id);Assert.Equal("Fictional payer",Cell(versions,row,"ClientName"));Assert.Equal("10001",Cell(versions,row,"NetCents"));Assert.Equal("750",Cell(versions,row,"TaxCents"));Assert.Equal("10751",Cell(versions,row,"TotalCents"));Assert.Equal(invoice.Hash,Cell(versions,row,"SnapshotSha256"));Assert.Equal("Succeeded",Cell(versions,row,"RecordedPdfStatus"));Assert.StartsWith("Documents",Cell(versions,row,"PdfRelativePath"));var detail=tables.Single(t=>t.Name=="Receipt snapshots");var receiptRow=detail.Rows.Single(r=>r[0]==receipt.Id);Assert.Equal("4000",Cell(detail,receiptRow,"PaymentAvailableCents"));Assert.Equal("2751",Cell(detail,receiptRow,"CustomerInvoiceBalanceCents"));Assert.Equal("8000",tables.Single(t=>t.Name=="Receipt applied amounts").Rows.Single()[5]);
    }
    [Fact]
    public void IssuedCreditNoteRetainsItsRelatedInvoiceAndExactFrozenLineFacts()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10001,0.075m);var book=new CreditNoteBook(f.Business.Store);var draft=book.SaveDraft(f.Business.Owner,new CreditNoteDraft{Name="Fictional readable credit",InvoiceVersionId=invoice.Id,IssueDate=f.Date,Portions=invoice.Snapshot.Lines.Select(l=>new InvoicePortion(l.ChargeId,l.Quantity)).ToArray()});var credit=book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableDocumentTables.Build(source,new());var versions=tables.Single(t=>t.Name=="Document versions");var row=versions.Rows.Single(r=>r[0]==credit.Id);Assert.Equal("CreditNote",Cell(versions,row,"Kind"));Assert.Equal(invoice.Snapshot.Number,Cell(versions,row,"RelatedDocument"));Assert.Equal(credit.Snapshot.TotalCents.ToString(),Cell(versions,row,"TotalCents"));var lines=tables.Single(t=>t.Name=="Document lines");Assert.Equal(credit.Snapshot.Lines.Single().RoundingCents.ToString(),Cell(lines,lines.Rows.Single(r=>r[0]==credit.Id),"RoundingCents"));
    }
    [Fact]
    public void EmptyDocumentTablesRetainTheirSchemasAndCapturedGeneration()
    {
        using var f=new LedgerFixture();using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableDocumentTables.Build(source,new());Assert.All(tables,table=>{Assert.Empty(table.Rows);Assert.NotEmpty(table.Columns);Assert.Equal(source.Source.GenerationId,table.GenerationId);Assert.Equal(source.Source.Watermark,table.Watermark);});
    }
    [Fact]
    public void AcceptedAndSupersededVersionsRemainSeparateFromLaterIssuedRevisions()
    {
        using var f=new LedgerFixture();ApplicationTests.Invoice(f,10000);var quotes=new QuoteBook(f.Business.Store);var original=quotes.Versions(f.Business.Owner).Single(v=>v.Snapshot.Kind==DocumentKind.Quote);var booking=new SarahBeauty.Business.Bookings.BookingBook(f.Business.Store).List(f.Business.Owner).Single();var draft=quotes.SaveDraft(f.Business.Owner,quotes.FromBooking(f.Business.Owner,booking,original.DocumentId) with{IssueDate=f.Date.AddDays(1),Terms="Fictional amended public terms"});var revised=quotes.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);quotes.Accept(f.Business.Owner,revised.Id,f.Date.AddDays(1),"Fictional reviewed amendment");
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var versions=ReadableDocumentTables.Build(source,new()).Single(t=>t.Name=="Document versions");var before=versions.Rows.Single(r=>r[0]==original.Id);var after=versions.Rows.Single(r=>r[0]==revised.Id);Assert.Equal("Superseded",Cell(versions,before,"Status"));Assert.Equal("1",Cell(versions,before,"Version"));Assert.Equal("Accepted",Cell(versions,after,"Status"));Assert.Equal("2",Cell(versions,after,"Version"));Assert.Equal(original.Hash,Cell(versions,before,"SnapshotSha256"));Assert.Equal(revised.Hash,Cell(versions,after,"SnapshotSha256"));
    }
}
