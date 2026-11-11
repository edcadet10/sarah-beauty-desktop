using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

internal static class QuoteSnapshot
{
    internal static ClientSnapshot Create(DbWork db,BusinessStore store,OwnerSession owner,DocumentDraft draft,string number,int version)
    {
        if(draft.Kind!=DocumentKind.Quote || draft.IssueDate is null)throw new ArgumentException("Choose a quote issue date.");
        if(draft.DueDate<draft.IssueDate)throw new ArgumentException("The quote expiry cannot precede its issue date.");
        var booking=new RecordStore<Booking>(store,"Booking").Get(db,owner,draft.BookingId);
        if(booking.Revision!=draft.BookingRevision)throw new ArgumentException("The booking changed. Refresh this draft from the current booking before issuing.");
        var customer=CustomerBook.Find(db,owner,draft.RecipientCustomerId)??throw new ArgumentException("Choose the quote recipient.");
        var totals=BookingRules.Totals(booking);
        if(totals.TotalCents is null || totals.Plan is null || !booking.Charges.Any(c=>c.Active))
            throw new ArgumentException("Resolve tax and the payment plan before issuing this quote.");
        var lines=booking.Charges.Where(c=>c.Active).Select(Line).ToArray();
        var plan=totals.Plan;
        ClientMilestone[] milestones=[new("Trial",plan.TrialCents,booking.TrialDue),
            new("Deposit",plan.DepositCents,booking.DepositDue),new("Final",plan.FinalCents,booking.FinalDue)];
        string name=(string)db.Scalar("SELECT DisplayName FROM Business WHERE Id=$id;",("$id",owner.BusinessId))!;
        return new(DocumentKind.Quote,number,version,"TEST",name,Party(customer),
            new(booking.Name,booking.EventDate,booking.Venue,booking.ReadyBy),draft.IssueDate.Value,draft.DueDate,draft.Terms,
            lines,totals.NetCents,totals.TaxCents!.Value,totals.TotalCents.Value,milestones.Where(m=>m.Cents>0).ToArray());
    }

    internal static ClientParty Party(Customer customer)=>new(customer.FullName,customer.BillingName,
        customer.BillingAddress,customer.Phone,customer.Email);

    private static ClientLine Line(BookingCharge charge)
    {
        long tax=Money.Multiply(charge.TaxRate!.Value,charge.NetCents);
        return new(charge.Id,charge.Description,charge.Unit,charge.Quantity,charge.UnitCents,charge.DiscountCents,
            charge.NetCents,tax,checked(charge.NetCents+tax));
    }
}
