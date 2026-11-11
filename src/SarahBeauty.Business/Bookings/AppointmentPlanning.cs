namespace SarahBeauty.Business.Bookings;

public static class AppointmentPlanning
{
    public static int? DurationMinutes(Booking booking,Appointment appointment)
    {
        var places=AppointmentRecipients.Places(booking,appointment.PlaceIds,requireActive:true);
        if(appointment.Kind=="Trial")return booking.HasTrial && places.Length<=1?booking.TrialDurationMinutes:null;
        if(places.Length==0)return null;
        var charges=booking.Charges.Where(c=>c.Active).ToDictionary(c=>c.Id);
        if(places.Any(p=>!charges.TryGetValue(p.ChargeId,out var c) || !c.DurationMinutes.HasValue))return null;
        return places.Sum(p=>charges[p.ChargeId].DurationMinutes!.Value);
    }

    public static DateTimeOffset EndFromDurations(Booking booking,Appointment appointment)
    {
        if(appointment.Start is null)throw new ArgumentException("Enter the appointment start before calculating its end.");
        int minutes=DurationMinutes(booking,appointment)??throw new ArgumentException("Resolve the selected recipients and their service durations, or enter the actual end time explicitly.");
        try{return appointment.Start.Value.AddMinutes(minutes);}
        catch(ArgumentOutOfRangeException){throw new ArgumentException("The planned duration exceeds the supported appointment date range.");}
    }
}
