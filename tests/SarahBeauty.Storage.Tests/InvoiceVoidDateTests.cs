using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class InvoiceVoidDateTests
{
    [Theory]
    [InlineData("customer",5450)][InlineData("tax",450)][InlineData("outstanding",5450)]
    public void LaterInvoiceVoidDoesNotEraseAmountsBeforeItsEffectiveDate(string report,long expected)
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,5000,.09m,dueDate:new(2027,1,14));SeedVoid(f,invoice,new(2027,1,20));
        var before=new DateOnly(2027,1,15);var financial=new FinancialReports(f.Business.Store).Load(f.Business.Owner,new(2027,1,1),before);
        long actual=report switch{"customer"=>financial.Customers.Sum(c=>c.OpenCents),"tax"=>financial.Tax.ChargedCents,_=>new PaymentApplications(f.Business.Store).Outstanding(f.Business.Owner,before).Sum(d=>d.Cents)};
        Assert.Equal(expected,actual);
        var after=new FinancialReports(f.Business.Store).Load(f.Business.Owner,new(2027,1,1),new(2027,1,20));Assert.Equal(0,after.Customers.Sum(c=>c.OpenCents));Assert.Equal(0,after.Tax.ChargedCents);Assert.Empty(new PaymentApplications(f.Business.Store).Outstanding(f.Business.Owner,new(2027,1,20)));
    }
    [Fact]
    public void HistoricalVisibilityDoesNotPermitNewBackdatedApplications()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,5000);var payment=f.Post(f.Draft(FinancialKind.Receipt,5000));SeedVoid(f,invoice,new(2027,1,20));
        var book=new PaymentApplications(f.Business.Store);var draft=book.SaveDraft(f.Business.Owner,new(){Name="Fictional blocked backdated application",PaymentId=payment.Id,InvoiceVersionId=invoice.Id,Cents=1000,Date=new(2027,1,15)});
        long watermark=Watermark(f);
        Assert.Equal(5000,book.Outstanding(f.Business.Owner,new(2027,1,15)).Sum(d=>d.Cents));
        var error=Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Contains("current issued invoice",error.Message);Assert.Empty(book.History(f.Business.Owner));Assert.Equal(watermark,Watermark(f));
    }

    [Fact]
    public void BookingAndCapturedCustomerRowsUseTheSameEffectiveBoundary()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,5000,.09m);SeedVoid(f,invoice,new(2027,1,20));
        var booking=new BookingBillingSources(f.Business.Store);var before=new DateOnly(2027,1,15);
        Assert.Equal(5450,Assert.Single(booking.Load(f.Business.Owner,invoice.BookingId,before).Invoices).OpenCents);
        Assert.Empty(booking.Load(f.Business.Owner,invoice.BookingId,new(2027,1,20)).Invoices);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var rows=ReadableCustomerReportTables.Build(source,new(),before).Single(t=>t.Name=="Report invoice amounts due");
        var row=Assert.Single(rows.Rows);Assert.Equal("5450",row[rows.Columns.ToList().IndexOf("Cents")]);Assert.Equal("450",row[rows.Columns.ToList().IndexOf("TaxCents")]);
        Assert.Empty(ReadableCustomerReportTables.Build(source,new(),new(2027,1,20)).Single(t=>t.Name=="Report invoice amounts due").Rows);
    }

    [Fact]
    public void ANewReceiptRetainsTheBalanceAtItsHistoricalIssueDate()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,5000);var payment=f.Post(f.Draft(FinancialKind.Receipt,1000));SeedVoid(f,invoice,new(2027,1,20));
        var book=new ReceiptBook(f.Business.Store);var draft=book.SaveDraft(f.Business.Owner,new(){Name="Fictional historical receipt",PaymentId=payment.Id,IssueDate=new(2027,1,15)});
        var issued=book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(5000,issued.Snapshot.Receipt!.CustomerInvoiceBalanceCents);
    }

    [Fact]
    public void AVoidedInvoiceWithoutItsRetainedDecisionFailsClosed()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,5000);
        f.Business.Files.Execute("UPDATE DocumentVersions SET Status='Void' WHERE Id='"+invoice.Id+"';");
        Assert.Throws<InvalidDataException>(()=>new PaymentApplications(f.Business.Store).Outstanding(f.Business.Owner,new(2027,1,15)));
    }

    private static long Watermark(LedgerFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));

    internal static DocumentDecision SeedVoid(LedgerFixture f,DocumentVersion invoice,DateOnly date)
    {
        var value=new DocumentDecision(1,Guid.NewGuid().ToString(),f.Business.Owner.BusinessId,invoice.DocumentId,invoice.Id,Guid.NewGuid().ToString(),DocumentDecisionAction.VoidInvoice,date,"Issued","Void",invoice.Hash,"Fictional historical invoice void",null,f.Business.Owner.OwnerId,"2027-01-20T12:00:00.0000000+00:00");
        string json=JsonSerializer.Serialize(value);f.Business.Store.Transaction(db=>{DocumentDecisionWrites.Insert(db,value,DocumentDecisionCodec.Payload(DocumentDecisionCodec.Draft(value)),json,DocumentDecisionCodec.Hash(json));DocumentDecisionWrites.ChangeStatus(db,value);return true;});return value;
    }
}
