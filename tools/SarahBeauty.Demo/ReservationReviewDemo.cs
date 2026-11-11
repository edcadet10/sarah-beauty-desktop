using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Demo;

internal static class ReservationReviewDemo
{
    internal static void Create(BusinessStore store,OwnerSession owner,string phrase,string customer)
    {
        DateOnly today=DateOnly.FromDateTime(DateTime.Today);var books=new BookingBook(store);
        var booking=Booking(store,owner,customer,today.AddDays(60));
        var original=Issue(store,owner,booking,today.AddDays(-1),phrase);
        var invoice=Invoice(store,owner,original,customer,today.AddDays(-1),original.Snapshot.Lines.Select(l=>new InvoicePortion(l.ChargeId,l.Quantity)).ToArray(),phrase);
        Pay(store,owner,booking.Id,customer,invoice.Id,today.AddDays(-1),phrase);
        booking=books.List(owner).Single(b=>b.Id==booking.Id);
        booking=new BookingReservations(store).Confirm(owner,booking.Id,booking.Revision,Guid.NewGuid().ToString(),today.AddDays(-1),"Fictional original paid reservation",phrase);
        booking=BookingRules.AddCharge(booking,"Fictional additional agreed service","Custom",1,6000);
        booking=books.Save(owner,booking with{Charges=booking.Charges.Select(c=>c with{TaxRate=0m}).ToArray()});
        var revised=Issue(store,owner,booking,today,phrase,original.DocumentId);
        var extra=revised.Snapshot.Lines.Single(l=>l.Description=="Fictional additional agreed service");
        Invoice(store,owner,revised,customer,today,[new(extra.ChargeId,1)],phrase);
    }

    private static Booking Booking(BusinessStore store,OwnerSession owner,string customer,DateOnly date)
    {
        var books=new BookingBook(store);var artist=books.SaveArtist(owner,new(){Name="Fictional reservation review artist"});
        var booking=BookingRules.Wedding("Fictional amended reservation rehearsal",0,0) with{PrimaryCustomerId=customer,PayerMode="Single",SinglePayerCustomerId=customer,
            UnpaidResponsibility="Fictional payer covers all agreed services",EventDate=date,Venue="Fictional reservation venue",ReadyBy="13:00"};
        var work=new Appointment(Guid.NewGuid().ToString(),"Wedding",artist.Id,ZonedTime.Resolve(date,"10:00","America/New_York",""),
            ZonedTime.Resolve(date,"12:00","America/New_York",""),"Fictional reservation venue"){TimeZoneId="America/New_York",PlaceIds=[booking.Places[0].Id]};
        return books.Save(owner,booking with{Charges=booking.Charges.Select(c=>c with{TaxRate=0m}).ToArray(),Appointments=[work]});
    }

    private static DocumentVersion Issue(BusinessStore store,OwnerSession owner,Booking booking,DateOnly date,string phrase,string document="")
    {
        var quotes=new QuoteBook(store);var draft=quotes.SaveDraft(owner,quotes.FromBooking(owner,booking,document) with{IssueDate=date});
        var quote=quotes.Issue(owner,draft.Id,draft.Revision,phrase);return quotes.Accept(owner,quote.Id,date,"Fictional accepted reservation review agreement");
    }

    private static DocumentVersion Invoice(BusinessStore store,OwnerSession owner,DocumentVersion quote,string customer,DateOnly date,InvoicePortion[] portions,string phrase)
    {
        var invoices=new InvoiceBook(store);var draft=invoices.SaveDraft(owner,new InvoiceDraft{Name="Fictional reservation review invoice",QuoteVersionId=quote.Id,
            PayerCustomerId=customer,IssueDate=date,Portions=portions});return invoices.Issue(owner,draft.Id,draft.Revision,phrase);
    }

    private static void Pay(BusinessStore store,OwnerSession owner,string booking,string customer,string invoice,DateOnly date,string phrase)
    {
        var chart=new ChartOfAccounts(store).List(owner).ToDictionary(a=>a.Code);var finance=new FinanceBook(store);
        var draft=finance.SaveDraft(owner,new(){Name="Fictional original reservation payment",Kind=FinancialKind.Receipt,Date=date,AmountCents=22500,TaxResolved=true,
            CustomerId=customer,BookingId=booking,CashAccountId=chart["1010"].Id,CounterAccountId=chart["4000"].Id});
        var receipt=finance.Approve(owner,draft.Id,draft.Revision,phrase);var apps=new PaymentApplications(store);
        var applied=apps.SaveDraft(owner,new(){Name="Fictional original reservation application",PaymentId=receipt.Id,InvoiceVersionId=invoice,Cents=22500,Date=date});
        apps.Approve(owner,applied.Id,applied.Revision,phrase);
    }
}
