using System.Globalization;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ClientDocumentStorageDateTests
{
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void QuoteAcceptanceKeepsItsStoredGregorianDate(string culture)
    {
        using var f=new LedgerFixture();var quote=Quote(f);WithCulture(culture,()=>new QuoteBook(f.Business.Store).Accept(f.Business.Owner,quote.Id,f.Date,"Fictional calendar-independent acceptance"));Assert.Equal("2027-01-10",f.Business.Store.Read(db=>(string)db.Scalar("SELECT AcceptedDate FROM DocumentVersions WHERE Id=$id;",("$id",quote.Id))!));Assert.Equal(f.Date,new QuoteBook(f.Business.Store).Versions(f.Business.Owner).Single(v=>v.Id==quote.Id).AcceptedDate);
    }
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void InvoiceMilestonesKeepTheirAcceptedDueDates(string culture)
    {
        using var f=new LedgerFixture();var quote=Quote(f);new QuoteBook(f.Business.Store).Accept(f.Business.Owner,quote.Id,f.Date,"Fictional accepted due dates");string id="";WithCulture(culture,()=>id=Invoice(f,quote).Id);Assert.Equal(f.Date.AddDays(30).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),f.Business.Store.Read(db=>(string)db.Scalar("SELECT DueDate FROM InvoiceMilestones WHERE InvoiceVersionId=$id;",("$id",id))!));
    }
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void CreditNoteHeadersKeepTheirActualEffectiveDate(string culture)
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000);var book=new CreditNoteBook(f.Business.Store);var draft=book.SaveDraft(f.Business.Owner,new(){Name="Fictional dated invoice reduction",InvoiceVersionId=invoice.Id,IssueDate=f.Date.AddDays(1),Portions=invoice.Snapshot.Lines.Select(v=>new InvoicePortion(v.ChargeId,v.Quantity/2)).ToArray()});string id="";WithCulture(culture,()=>id=book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Id);Assert.Equal("2027-01-11",f.Business.Store.Read(db=>(string)db.Scalar("SELECT Date FROM CreditNoteHeaders WHERE VersionId=$id;",("$id",id))!));
    }
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void ReceiptHistoryIncludesOnlyApplicationsAndRefundsThroughItsIssueDate(string culture)
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000);var payment=f.Post(f.Draft(FinancialKind.Receipt,10000));CancellationSourceTests.Apply(f,payment.Id,invoice,5000);var apps=new PaymentApplications(f.Business.Store);var application=apps.SaveDraft(f.Business.Owner,new(){Name="Fictional later application",PaymentId=payment.Id,InvoiceVersionId=invoice.Id,Cents=2500,Date=f.Date.AddDays(1)});apps.Approve(f.Business.Owner,application.Id,application.Revision,BusinessFixture.Phrase);f.Post(f.Draft(FinancialKind.Refund,1000) with{OriginalId=payment.Id,Date=f.Date.AddDays(2)});var book=new ReceiptBook(f.Business.Store);var draft=book.SaveDraft(f.Business.Owner,new(){Name="Fictional earlier receipt",PaymentId=payment.Id,IssueDate=f.Date});long watermark=Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));WithCulture(culture,()=>{var preview=book.Preview(f.Business.Owner,draft);Assert.Equal(5000,Assert.Single(preview.Receipt!.Applications).Cents);Assert.Equal(0,preview.Receipt.RefundCents);});Assert.Equal(watermark,TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));
    }
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void ReversedPaymentsCannotAcquireANewReceiptAfterAChangeOfCalendar(string culture)
    {
        using var f=new LedgerFixture();var payment=f.Post(f.Draft(FinancialKind.Receipt,10000));f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=payment.Id});var book=new ReceiptBook(f.Business.Store);var draft=book.SaveDraft(f.Business.Owner,new(){Name="Fictional blocked reversed payment",PaymentId=payment.Id,IssueDate=f.Date});long watermark=Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));WithCulture(culture,()=>Assert.Contains("reversed",Assert.Throws<ArgumentException>(()=>book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase)).Message,StringComparison.Ordinal));Assert.Equal(watermark,TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Empty(new QuoteBook(f.Business.Store).Versions(f.Business.Owner));
    }
    private static DocumentVersion Quote(LedgerFixture f)
    {
        var booking=BookingRules.AddCharge(new Booking{Name="Fictional dated client agreement",HasTrial=false,TrialCents=0,CustomDepositCents=0,FinalDue=f.Date.AddDays(30),PrimaryCustomerId=f.CustomerId,PayerMode="Single",SinglePayerCustomerId=f.CustomerId,UnpaidResponsibility="Fictional payer agreement"},"Fictional service","Custom",1,10000);booking=new BookingBook(f.Business.Store).Save(f.Business.Owner,booking with{Charges=booking.Charges.Select(v=>v with{TaxRate=0m}).ToArray()});var book=new QuoteBook(f.Business.Store);var draft=book.SaveDraft(f.Business.Owner,book.FromBooking(f.Business.Owner,booking) with{IssueDate=f.Date});return book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }
    private static DocumentVersion Invoice(LedgerFixture f,DocumentVersion quote)
    {
        var book=new InvoiceBook(f.Business.Store);var draft=book.SaveDraft(f.Business.Owner,new(){Name="Fictional calendar invoice",QuoteVersionId=quote.Id,PayerCustomerId=f.CustomerId,IssueDate=f.Date,Portions=quote.Snapshot.Lines.Select(v=>new InvoicePortion(v.ChargeId,v.Quantity)).ToArray()});return book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }
    private static void WithCulture(string name,Action action)
    {
        var previous=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(name);action();}finally{CultureInfo.CurrentCulture=previous;}
    }
}
