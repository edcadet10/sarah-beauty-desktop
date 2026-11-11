using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Demo;

internal static class CancellationDemo
{
    internal static void Create(BusinessStore store,OwnerSession owner,string phrase,string customer)
    {
        var date=new DateOnly(2027,1,10);
        var booking=BookingRules.AddCharge(new Booking{Name="Fictional cancellation rehearsal",HasTrial=false,TrialCents=0,CustomDepositCents=0,
            PrimaryCustomerId=customer,PayerMode="Single",SinglePayerCustomerId=customer,UnpaidResponsibility="Fictional agreed payer"},"Fictional cancellation service","Custom",1,10000);
        booking=new BookingBook(store).Save(owner,booking with{Charges=booking.Charges.Select(c=>c with{TaxRate=0.1m}).ToArray()});
        var invoice=Invoice(store,owner,phrase,booking,date);
        var chart=new ChartOfAccounts(store).List(owner).ToDictionary(a=>a.Code);var finance=new FinanceBook(store);
        var draft=finance.SaveDraft(owner,new(){Name="Fictional cancellation receipt",Kind=FinancialKind.Receipt,Date=date,AmountCents=11000,TaxCents=1000,
            TaxResolved=true,CustomerId=customer,BookingId=booking.Id,CashAccountId=chart["1010"].Id,CounterAccountId=chart["4000"].Id,PaymentMethod="Fictional bank receipt",EvidenceId="Fictional receipt evidence"});
        var receipt=finance.Approve(owner,draft.Id,draft.Revision,phrase);var applications=new PaymentApplications(store);
        var application=applications.SaveDraft(owner,new(){Name="Fictional cancellation allocation",PaymentId=receipt.Id,InvoiceVersionId=invoice.Id,Cents=11000,Date=date});
        applications.Approve(owner,application.Id,application.Revision,phrase);
    }

    private static DocumentVersion Invoice(BusinessStore store,OwnerSession owner,string phrase,Booking booking,DateOnly date)
    {
        var quotes=new QuoteBook(store);var draft=quotes.SaveDraft(owner,quotes.FromBooking(owner,booking) with{IssueDate=date,Terms="Fictional issued terms. Any cancellation outcome needs a separate evidenced agreement."});
        var quote=quotes.Issue(owner,draft.Id,draft.Revision,phrase);quotes.Accept(owner,quote.Id,date,"Fictional cancellation fixture acceptance");
        var invoices=new InvoiceBook(store);var invoice=invoices.SaveDraft(owner,new(){Name="Fictional cancellation invoice",QuoteVersionId=quote.Id,
            PayerCustomerId=booking.PrimaryCustomerId,IssueDate=date,Portions=quote.Snapshot.Lines.Select(l=>new InvoicePortion(l.ChargeId,l.Quantity)).ToArray()});
        return invoices.Issue(owner,invoice.Id,invoice.Revision,phrase);
    }
}
