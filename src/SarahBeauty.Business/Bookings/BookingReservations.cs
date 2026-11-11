using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Bookings;

public sealed class BookingReservations(BusinessStore store)
{
    internal Action<string>? FailurePoint {get;set;}

    public Booking Confirm(OwnerSession owner,string bookingId,int revision,string requestId,DateOnly date,string reason,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        if(!Guid.TryParse(requestId,out _) || string.IsNullOrWhiteSpace(reason))throw new ArgumentException("Record a reservation reason and valid request identity.");
        return store.Transaction(db=>Confirm(db,owner,bookingId,revision,requestId,date,reason.Trim()));
    }

    private Booking Confirm(DbWork db,OwnerSession owner,string bookingId,int revision,string requestId,DateOnly date,string reason)
    {
        store.RequireSession(db,owner);
        var book=new BookingBook(store);
        var booking=book.Records.Get(db,owner,bookingId);
        var prior=booking.Reservations.SingleOrDefault(r=>r.RequestId==requestId);
        if(prior is not null)
        {
            if(prior.Date!=date || prior.Reason!=reason)throw new ArgumentException("This reservation request was already used with different details.");
            return booking;
        }
        if(booking.Revision!=revision)throw new ArgumentException("Reload the changed booking before reserving it.");
        if(booking.Status!="Awaiting deposit")throw new ArgumentException("Choose an accepted booking awaiting its deposit.");
        var quote=DocumentVersions.Find(db,owner,id:booking.AcceptedQuoteVersionId)
            ??throw new ArgumentException("Accept an exact quote version before reserving the date.");
        ReservationRules.ValidateAgreement(booking,quote,date);
        ReservationRules.ValidateSchedule(booking);
        BookingValidation.Validate(db,owner,booking,book.Records.List(db,owner).Where(b=>b.Id!=booking.Id).ToList(),
            new RecordStore<Artist>(store,"Artist").List(db,owner));
        var paid=PaidMilestones(db,owner,booking.Id,date);
        long trial=quote.Snapshot.Milestones.Where(m=>m.Kind=="Trial").Sum(m=>m.Cents);
        long deposit=quote.Snapshot.Milestones.Where(m=>m.Kind=="Deposit").Sum(m=>m.Cents);
        if(paid.Trial<trial || paid.Deposit<deposit)throw new ArgumentException("Apply approved receipts or accepted funded credits to the required trial and booking deposit before reserving.");
        var reservation=new BookingReservation(requestId,quote.Id,date,trial,deposit,reason,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));
        var result=book.Transition(db,owner,booking with{Reservations=[..booking.Reservations,reservation]},"Confirmed",reason);
        FailurePoint?.Invoke("reservation");
        BusinessStore.Audit(db,owner,"Confirm booking reservation",booking.Id);
        return result;
    }

    private static (long Trial,long Deposit) PaidMilestones(DbWork db,OwnerSession owner,string bookingId,DateOnly date)
    {
        var rows=db.Read("""
            SELECT M.Kind,A.Cents,A.ReversesId FROM PaymentApplications A
            JOIN InvoiceMilestones M ON M.Id=A.MilestoneId JOIN DocumentVersions V ON V.Id=A.InvoiceVersionId
            JOIN Documents D ON D.Id=V.DocumentId WHERE D.BusinessId=$business AND D.BookingId=$booking AND A.Date<=$date;
            """,r=>(Kind:r.GetString(0),Cents:r.GetInt64(1)*(r.IsDBNull(2)?1:-1)),
            ("$business",owner.BusinessId),("$booking",bookingId),("$date",date.ToString("yyyy-MM-dd")));
        return(rows.Where(r=>r.Kind=="Trial").Sum(r=>r.Cents),rows.Where(r=>r.Kind=="Deposit").Sum(r=>r.Cents));
    }
}
