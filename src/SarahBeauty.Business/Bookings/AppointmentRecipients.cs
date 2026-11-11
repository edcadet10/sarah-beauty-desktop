namespace SarahBeauty.Business.Bookings;

internal static class AppointmentRecipients
{
    internal static BookingPlace[] Places(Booking booking,string[] ids,bool requireActive)
    {
        if(ids.Distinct().Count()!=ids.Length)throw new ArgumentException("Select each appointment recipient place once.");
        var places=booking.Places.Where(p=>!requireActive || p.Active).ToDictionary(p=>p.Id);
        if(ids.Any(id=>!places.ContainsKey(id)))throw new ArgumentException("Choose recipient places from this booking, retaining active places for active work.");
        return ids.Select(id=>places[id]).ToArray();
    }

    internal static HashSet<string> Identities(Booking booking,Appointment appointment)=>Places(booking,appointment.PlaceIds,requireActive:false)
        .Select(p=>p.CustomerId.Length>0?"Customer:"+p.CustomerId:"Place:"+booking.Id+":"+p.Id).ToHashSet();
}
