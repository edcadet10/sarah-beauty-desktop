using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Dashboard;

internal static class DashboardReservationReview
{
    internal static void Check(DbWork db,OwnerSession owner,Booking booking,DocumentVersion quote,DateOnly asOf,List<string> issues)=>Check(db,owner.BusinessId,booking,quote,asOf,issues);
    internal static void Check(DbWork db,string businessId,Booking booking,DocumentVersion quote,DateOnly asOf,List<string> issues)
    {
        try{ReservationRules.ValidateAgreement(booking,quote,asOf);}catch(ArgumentException error){issues.Add(error.Message);}
        try{ReservationRules.ValidateSchedule(booking);}catch(ArgumentException error){issues.Add(error.Message);}
        var review=booking.Reservations.LastOrDefault();
        if(review?.QuoteVersionId!=quote.Id)issues.Add("The current accepted agreement needs its reservation review.");
        else if(review.Date>asOf)issues.Add("The latest reservation review is dated after the planning as-of date.");
        var paid=BookingReservations.PaidMilestones(db,businessId,booking.Id,asOf);
        if(paid.Trial<quote.Snapshot.Milestones.Where(m=>m.Kind=="Trial").Sum(m=>m.Cents)
            || paid.Deposit<quote.Snapshot.Milestones.Where(m=>m.Kind=="Deposit").Sum(m=>m.Cents))
            issues.Add("Approved trial or booking-deposit allocations need review for the current agreement.");
    }
}
