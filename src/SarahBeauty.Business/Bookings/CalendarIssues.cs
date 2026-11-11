using SarahBeauty.Business.Rules;

namespace SarahBeauty.Business.Bookings;

internal static class CalendarIssues
{
    internal static List<CalendarIssue> Read(CalendarWork work,Dictionary<string,Artist> artists,IEnumerable<CalendarIssue> conflicts)
    {
        var issues=conflicts.ToList();var appointment=work.Appointment;
        if(!work.HasTimes)issues.Add(new("TimeUndecided","Appointment times need review."));
        if(appointment.TimeZoneId.Length==0)issues.Add(new("ZoneUnconfirmed","The stated appointment time zone remains unconfirmed."));
        if(appointment.PlaceIds.Length==0)issues.Add(new("RecipientsUndecided","Appointment recipient places remain undecided."));
        if(string.IsNullOrWhiteSpace(appointment.Location))issues.Add(new("LocationUndecided","Appointment location remains undecided."));
        if(!artists.TryGetValue(appointment.ArtistId,out var artist))issues.Add(new("ArtistUnavailable","The original artist record needs review."));
        else if(artist.Archived && appointment.Status=="Planned" && !work.Cancelled)issues.Add(new("ArtistArchived","Reassign planned work or review the archived artist."));
        if(!work.Cancelled && appointment.Kind==(work.Booking.EventType=="Wedding"?"Wedding":"Other"))Readiness(work,issues);
        return issues;
    }

    private static void Readiness(CalendarWork work,List<CalendarIssue> issues)
    {
        var booking=work.Booking;var appointment=work.Appointment;
        try
        {
            var deadline=BookingSchedule.ReadyBy(booking);
            if(!deadline.HasValue)issues.Add(new("ReadinessUndecided","The event date or ready-by time remains undecided."));
            else if(appointment.End>deadline)issues.Add(new("AfterReadyBy","Appointment ends after the recorded ready-by time.",!string.IsNullOrWhiteSpace(appointment.OverrideReason)));
            if(appointment.Start.HasValue && booking.EventDate.HasValue && DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(appointment.Start.Value,ZonedTime.Zone(booking.EventTimeZoneId)).DateTime)!=booking.EventDate)
                issues.Add(new("EventDateMismatch","The appointment's event-local date differs from the booking date."));
        }
        catch(ArgumentException){issues.Add(new("ReadinessNeedsReview","The recorded event time zone, local time, or UTC offset needs review."));}
    }
}
