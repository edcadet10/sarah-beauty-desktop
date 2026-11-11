namespace SarahBeauty.Business.Bookings;

internal static class AppointmentRules
{
    internal static void Validate(Booking booking, IReadOnlyList<Booking> others, IReadOnlyList<Artist> artists)
    {
        if (booking.Appointments.Select(a => a.Id).Distinct().Count() != booking.Appointments.Length)
            throw new ArgumentException("Each appointment needs a distinct identity.");
        var scheduled=others.Append(booking).Where(b=>b.Status!="Cancelled").SelectMany(b=>b.Appointments.Where(a=>a.Status!="Cancelled").Select(a=>(Booking:b,Appointment:a))).ToArray();
        foreach (var appointment in booking.Appointments)
        {
            if (!artists.Any(a => a.Id == appointment.ArtistId && (!a.Archived || appointment.Status!="Planned")))
                throw new ArgumentException("Select an existing artist, using an active artist for planned work.");
            if (!new[] { "Trial", "Wedding", "Other" }.Contains(appointment.Kind)
                || !new[] { "Planned", "Completed", "Cancelled" }.Contains(appointment.Status))
                throw new ArgumentException("Choose a supported appointment kind and status.");
            if (appointment.Start.HasValue != appointment.End.HasValue || appointment.End <= appointment.Start
                || (appointment.Status == "Completed" && !appointment.Start.HasValue))
                throw new ArgumentException("Enter valid start and end times. Completed work needs both times.");
            AppointmentRecipients.Places(booking,appointment.PlaceIds,requireActive:appointment.Status!="Cancelled");
            ValidateConflicts(booking,appointment,scheduled);
        }
    }

    private static void ValidateConflicts(Booking booking,Appointment appointment,(Booking Booking,Appointment Appointment)[] scheduled)
    {
        if(booking.Status=="Cancelled" || appointment.Status=="Cancelled" || appointment.Start is null)return;
        var recipients=AppointmentRecipients.Identities(booking,appointment);
        var overlaps=scheduled.Where(pair=>!(pair.Booking.Id==booking.Id && pair.Appointment.Id==appointment.Id)
            && pair.Appointment.Start<appointment.End && pair.Appointment.End>appointment.Start).ToArray();
        var uncovered=overlaps.Where(pair=>string.IsNullOrWhiteSpace(appointment.OverrideReason)
            && (pair.Booking.Id!=booking.Id || string.IsNullOrWhiteSpace(pair.Appointment.OverrideReason))).ToArray();
        if(uncovered.Any(pair=>pair.Appointment.ArtistId==appointment.ArtistId))
            throw new ArgumentException("This artist has overlapping work. Change the times or record an override reason.");
        if(uncovered.Any(pair=>recipients.Overlaps(AppointmentRecipients.Identities(pair.Booking,pair.Appointment))))
            throw new ArgumentException("An appointment recipient has overlapping work. Change the times or record an explicit override reason.");
    }
}
