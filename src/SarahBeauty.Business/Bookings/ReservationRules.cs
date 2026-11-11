using SarahBeauty.Business.Documents;

namespace SarahBeauty.Business.Bookings;

internal static class ReservationRules
{
    internal static void ValidateAgreement(Booking booking,DocumentVersion quote,DateOnly date)
    {
        if(quote.BookingId!=booking.Id || quote.Status!="Accepted" || quote.AcceptedDate>date)
            throw new ArgumentException("The booking needs a quote accepted on or before its reservation date.");
        var snapshot=quote.Snapshot;
        var totals=BookingRules.Totals(booking);
        if(snapshot.Event!=QuoteSnapshot.Event(booking)
            || totals.TotalCents!=snapshot.TotalCents || totals.TaxCents!=snapshot.TaxCents
            || !snapshot.Lines.SequenceEqual(booking.Charges.Where(c=>c.Active).Select(QuoteSnapshot.Line))
            || totals.Plan is null || totals.Plan.TrialCents!=snapshot.Milestones.Where(m=>m.Kind=="Trial").Sum(m=>m.Cents)
            || totals.Plan.DepositCents!=snapshot.Milestones.Where(m=>m.Kind=="Deposit").Sum(m=>m.Cents))
            throw new ArgumentException("The event, charges, or plan changed. Issue and accept the revised quote before reserving.");
        if(booking.PrimaryCustomerId.Length==0 || booking.PayerMode=="Undecided" || string.IsNullOrWhiteSpace(booking.UnpaidResponsibility))
            throw new ArgumentException("Resolve the contact, payer arrangement, and unpaid-share responsibility.");
    }

    internal static void ValidateSchedule(Booking booking)
    {
        if(booking.EventDate is null || string.IsNullOrWhiteSpace(booking.Venue) || string.IsNullOrWhiteSpace(booking.ReadyBy))
            throw new ArgumentException("Resolve the event date, venue, and ready-by local time.");
        var zone=Rules.ZonedTime.Zone(booking.EventTimeZoneId);
        var deadline=BookingSchedule.ReadyBy(booking)!.Value;
        var work=booking.Appointments.Where(a=>a.Status!="Cancelled" && a.Kind!=(booking.EventType=="Wedding"?"Other":"Wedding") && a.Kind!="Trial").ToList();
        if(work.Count==0)throw new ArgumentException("Schedule the event with an artist before reserving.");
        foreach(var appointment in work)
        {
            if(appointment.Start is null || appointment.End is null || string.IsNullOrWhiteSpace(appointment.Location)
                || DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(appointment.Start.Value,zone).DateTime)!=booking.EventDate)
                throw new ArgumentException("Resolve each event appointment's date, times, and location.");
            if(appointment.End>deadline && string.IsNullOrWhiteSpace(appointment.OverrideReason))
                throw new ArgumentException("An event appointment ends after the ready-by time. Change it or record an override reason.");
        }
    }
}
