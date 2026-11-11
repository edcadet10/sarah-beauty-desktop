using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Bookings;

public sealed class CalendarSources(BusinessStore store)
{
    internal Action? AfterBookingsRead {get;set;}

    public CalendarSnapshot Load(OwnerSession owner,DateOnly from,DateOnly through,string timeZoneId="America/New_York",string artistId="",bool includeCancelled=false)=>store.ReadSnapshot(db=>
    {
        store.RequireSession(db,owner);
        if(from>through)throw new ArgumentException("The calendar start must be on or before its end.");
        var zone=ZonedTime.Zone(timeZoneId);
        var bookings=new RecordStore<Booking>(store,"Booking").List(db,owner);AfterBookingsRead?.Invoke();
        var artists=new RecordStore<Artist>(store,"Artist").List(db,owner).ToDictionary(a=>a.Id);
        if(artistId.Length>0 && !artists.ContainsKey(artistId))throw new ArgumentException("Choose an artist from this business.");
        var names=db.Read("SELECT Id,json_extract(DataJson,'$.FullName') FROM Customers WHERE BusinessId=$business;",r=>(Id:r.GetString(0),Name:r.GetString(1)),("$business",owner.BusinessId)).ToDictionary(c=>c.Id,c=>c.Name);
        var all=bookings.SelectMany(b=>b.Appointments.Select(a=>new CalendarWork(b,a,AppointmentRecipients.Identities(b,a)))).ToArray();
        var conflicts=CalendarConflicts.Build(all);
        var selected=all.Where(w=>(includeCancelled || !w.Cancelled) && (artistId.Length==0 || w.Appointment.ArtistId==artistId)).ToArray();
        var scheduled=selected.Where(w=>w.HasTimes && InWindow(w,zone,from,through)).OrderBy(w=>w.Appointment.Start).ThenBy(w=>w.Booking.Name)
            .Select(w=>Slot(w,zone,names,artists,conflicts.GetValueOrDefault(w.Key)??[])).ToArray();
        var unscheduled=selected.Where(w=>!w.HasTimes).OrderBy(w=>w.Booking.Name).Select(w=>Slot(w,zone,names,artists,[])).ToArray();
        var events=bookings.Where(b=>(includeCancelled || b.Status!="Cancelled") && (artistId.Length==0 || b.Appointments.Any(a=>a.ArtistId==artistId && (includeCancelled || a.Status!="Cancelled")))).ToArray();
        return new CalendarSnapshot((string)db.Scalar("SELECT DisplayName FROM Business WHERE Id=$id;",("$id",owner.BusinessId))!,
            (string)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!,Convert.ToInt64(db.Scalar("SELECT Watermark FROM Business WHERE Id=$id;",("$id",owner.BusinessId))),
            DateTimeOffset.UtcNow.ToString("O"),from,through,zone.Id,scheduled,unscheduled,
            events.Where(b=>b.EventDate>=from && b.EventDate<=through).OrderBy(b=>b.EventDate).Select(Event).ToArray(),events.Where(b=>!b.EventDate.HasValue).Select(Event).ToArray());
    });

    private static bool InWindow(CalendarWork work,TimeZoneInfo zone,DateOnly from,DateOnly through)
    {
        var first=TimeZoneInfo.ConvertTime(work.Appointment.Start!.Value,zone).Date;
        var last=TimeZoneInfo.ConvertTime(work.Appointment.End!.Value.AddTicks(-1),zone).Date;
        return DateOnly.FromDateTime(first)<=through && DateOnly.FromDateTime(last)>=from;
    }

    private static CalendarSlot Slot(CalendarWork work,TimeZoneInfo zone,Dictionary<string,string> names,Dictionary<string,Artist> artists,IEnumerable<CalendarIssue> conflicts)
    {
        var booking=work.Booking;var a=work.Appointment;var charges=booking.Charges.ToDictionary(c=>c.Id);
        var recipients=AppointmentRecipients.Places(booking,a.PlaceIds,requireActive:false).Select(p=>new CalendarRecipient(p.Id,charges[p.ChargeId].Description,p.Ordinal,p.CustomerId,
            p.CustomerId.Length==0?"Unnamed":names.GetValueOrDefault(p.CustomerId,"Customer unavailable"),p.Active)).ToArray();
        return new(booking.Id,booking.Name,booking.Status,a.Id,a.Kind,a.Status,a.ArtistId,artists.GetValueOrDefault(a.ArtistId)?.Name??"Artist unavailable",a.Location,
            a.Start.HasValue?TimeZoneInfo.ConvertTime(a.Start.Value,zone):null,a.End.HasValue?TimeZoneInfo.ConvertTime(a.End.Value,zone):null,a.Start,a.End,a.TimeZoneId,recipients,CalendarIssues.Read(work,artists,conflicts).ToArray())
            {OverrideReason=a.OverrideReason};
    }

    private static CalendarBooking Event(Booking booking)
    {
        bool scheduled=false;
        if(booking.Status!="Cancelled" && booking.EventDate.HasValue)
        {
            var zone=ZonedTime.Zone(booking.EventTimeZoneId);
            scheduled=booking.Appointments.Any(a=>a.Status!="Cancelled" && a.Start.HasValue && a.End>a.Start && a.Kind==(booking.EventType=="Wedding"?"Wedding":"Other")
                && DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(a.Start.Value,zone).DateTime)==booking.EventDate);
        }
        return new(booking.Id,booking.Name,booking.Status,booking.EventDate,booking.EventTimeZoneId,booking.Venue,booking.ReadyBy,scheduled);
    }
}
