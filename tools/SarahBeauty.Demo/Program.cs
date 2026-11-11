using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Demo;

internal static class Program
{
    // Public fictional fixture only. Never use this phrase for real owner accounts.
    private const string Phrase="fictional meadow lantern window";

    private static int Main(string[] args)
    {
        if(args.Length is not (2 or 3) || args[0]!="--output" || args.Length==3 && args[2]!="--cancellation")
        {Console.Error.WriteLine("Use --output NEW_EMPTY_DIRECTORY [--cancellation].");return 2;}
        try
        {
            string folder=Path.GetFullPath(args[1]);
            if(Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
                throw new ArgumentException("Use a new empty directory for the fictional demo.");
            Directory.CreateDirectory(folder);
            var store=new BusinessStore(Path.Combine(folder,"sarahbeauty.db"));
            var owner=new OwnerAccess(store).Setup("Fictional Sarah Beauty Demo","Alex Example",Phrase,"Morgan Example",Phrase);
            var booking=Wedding(store,owner);
            Documents(store,owner,booking,folder);
            if(args.Length==3)CancellationDemo.Create(store,owner,Phrase,booking.PrimaryCustomerId);
            Console.WriteLine("Fictional TEST demo created at "+folder);
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error.Message);return 1;}
    }

    private static Booking Wedding(BusinessStore store,OwnerSession owner)
    {
        var customer=new CustomerBook(store).Save(owner,new Customer{FullName="Jordan Avery Example",BillingName="Jordan Avery Example",
            BillingAddress="123 Fictional Avenue\nExample City, ST 00000",Email="jordan@example.com",PrivateNotes="PRIVATE-DEMO-NOTE"});
        var booking=BookingRules.Wedding("Fictional wedding",9,76.6m) with{PrimaryCustomerId=customer.Id,
            PayerMode="Single",SinglePayerCustomerId=customer.Id,UnpaidResponsibility="Fictional payer covers all agreed charges",
            EventDate=DateOnly.FromDateTime(DateTime.Today).AddDays(30),Venue="Fictional Garden Venue",ReadyBy="12:00"};
        for(int i=0;i<50;i++)booking=BookingRules.AddCharge(booking,$"Fictional add-on {i+1}: a longer description for a multi-page layout review","Custom",1,100);
        return new BookingBook(store).Save(owner,booking with{Charges=booking.Charges.Select(c=>c with{TaxRate=0m}).ToArray()});
    }

    private static void Documents(BusinessStore store,OwnerSession owner,Booking booking,string folder)
    {
        var quotes=new QuoteBook(store);
        var draft=quotes.SaveDraft(owner,quotes.FromBooking(owner,booking) with{Terms="Fictional TEST terms only. Actual dates, tax treatment, and cancellation decisions require owner review. Sending is manual."});
        var quote=quotes.Issue(owner,draft.Id,draft.Revision,Phrase);
        quotes.Accept(owner,quote.Id,quote.Snapshot.IssueDate,"Fictional acceptance for a software rehearsal");
        var invoices=new InvoiceBook(store);
        var invoiceDraft=invoices.SaveDraft(owner,new InvoiceDraft{Name="Fictional payer invoice",QuoteVersionId=quote.Id,
            PayerCustomerId=booking.PrimaryCustomerId,IssueDate=quote.Snapshot.IssueDate,Terms=quote.Snapshot.Terms,
            Portions=quote.Snapshot.Lines.Select(l=>new InvoicePortion(l.ChargeId,l.Quantity)).ToArray()});
        var invoice=invoices.Issue(owner,invoiceDraft.Id,invoiceDraft.Revision,Phrase);
        var artifacts=new DocumentArtifacts(store);
        File.Copy(artifacts.Render(owner,quote.Id),Path.Combine(folder,"quote-preview.pdf"));
        File.Copy(artifacts.Render(owner,invoice.Id),Path.Combine(folder,"invoice-preview.pdf"));
        PaymentDocuments(store,owner,invoice,booking,folder);
    }

    private static void PaymentDocuments(BusinessStore store,OwnerSession owner,DocumentVersion invoice,Booking booking,string folder)
    {
        var notes=new CreditNoteBook(store);
        var adjustment=notes.SaveDraft(owner,new CreditNoteDraft{Name="Fictional agreed removal of one add-on",InvoiceVersionId=invoice.Id,
            IssueDate=invoice.Snapshot.IssueDate,Portions=[new(invoice.Snapshot.Lines.Last().ChargeId,1)]});
        var note=notes.Issue(owner,adjustment.Id,adjustment.Revision,Phrase);
        var payment=Payment(store,owner,booking,invoice.Snapshot.IssueDate);
        var applications=new PaymentApplications(store);
        var allocation=applications.SaveDraft(owner,new PaymentApplicationDraft{Name="Fictional invoice allocation",PaymentId=payment.Id,
            InvoiceVersionId=invoice.Id,Date=invoice.Snapshot.IssueDate,Cents=20000});
        applications.Approve(owner,allocation.Id,allocation.Revision,Phrase);
        var receipts=new ReceiptBook(store);
        var draft=receipts.SaveDraft(owner,new ReceiptDraft{Name="Fictional payment receipt",PaymentId=payment.Id,
            IssueDate=invoice.Snapshot.IssueDate,ClientReference="Fictional payment reference"});
        var receipt=receipts.Issue(owner,draft.Id,draft.Revision,Phrase);
        var artifacts=new DocumentArtifacts(store);
        File.Copy(artifacts.Render(owner,note.Id),Path.Combine(folder,"credit-note-preview.pdf"));
        File.Copy(artifacts.Render(owner,receipt.Id),Path.Combine(folder,"receipt-preview.pdf"));
    }

    private static PostedMovement Payment(BusinessStore store,OwnerSession owner,Booking booking,DateOnly date)
    {
        var chart=new ChartOfAccounts(store);
        chart.Initialize(owner);
        var accounts=chart.List(owner).ToDictionary(a=>a.Code);
        var money=new FinanceBook(store);
        var draft=money.SaveDraft(owner,new FinancialDraft{Name="Fictional card payment",Kind=FinancialKind.Receipt,Date=date,
            AmountCents=25000,FeeCents=750,TaxResolved=true,CashAccountId=accounts["1020"].Id,CounterAccountId=accounts["4000"].Id,
            CustomerId=booking.PrimaryCustomerId,BookingId=booking.Id,PaymentMethod="Card"});
        return money.Approve(owner,draft.Id,draft.Revision,Phrase);
    }
}
