namespace SarahBeauty.Business.Bookings;

public sealed record CalendarRecipient(string PlaceId,string Service,int Ordinal,string CustomerId,string Name,bool Active);
public sealed record CalendarIssue(string Code,string Message,bool OverrideRecorded=false,string RelatedBookingId="",string RelatedAppointmentId="");
public sealed record CalendarSlot(string BookingId,string BookingName,string BookingStatus,string AppointmentId,string Kind,string Status,
    string ArtistId,string ArtistName,string Location,DateTimeOffset? Start,DateTimeOffset? End,DateTimeOffset? StatedStart,DateTimeOffset? StatedEnd,string StatedTimeZoneId,
    CalendarRecipient[] Recipients,CalendarIssue[] Issues)
{
    public string OverrideReason {get;init;}="";
    public bool Cancelled=>BookingStatus=="Cancelled" || Status=="Cancelled";
}
public sealed record CalendarBooking(string Id,string Name,string Status,DateOnly? EventDate,string TimeZoneId,string Venue,string ReadyBy,bool EventWorkScheduled);
public sealed record CalendarSnapshot(string BusinessName,string Mode,long Watermark,string GeneratedAtUtc,DateOnly From,DateOnly Through,string TimeZoneId,
    CalendarSlot[] Scheduled,CalendarSlot[] Unscheduled,CalendarBooking[] Events,CalendarBooking[] DateUndecided);

internal sealed record CalendarWork(Booking Booking,Appointment Appointment,HashSet<string> People)
{
    internal (string Booking,string Appointment) Key=>(Booking.Id,Appointment.Id);
    internal bool Cancelled=>Booking.Status=="Cancelled" || Appointment.Status=="Cancelled";
    internal bool HasTimes=>Appointment.Start.HasValue && Appointment.End.HasValue && Appointment.End>Appointment.Start;
}
