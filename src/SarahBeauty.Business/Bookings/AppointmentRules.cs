namespace SarahBeauty.Business.Bookings;

internal static class AppointmentRules
{
    internal static void Validate(Booking booking, IReadOnlyList<Booking> others, IReadOnlyList<Artist> artists)
    {
        if (booking.Appointments.Select(a => a.Id).Distinct().Count() != booking.Appointments.Length)
            throw new ArgumentException("Each appointment needs a distinct identity.");
        foreach (var appointment in booking.Appointments)
        {
            if (!artists.Any(a => a.Id == appointment.ArtistId && !a.Archived))
                throw new ArgumentException("Select an active artist for each appointment.");
            if (!new[] { "Trial", "Wedding", "Other" }.Contains(appointment.Kind)
                || !new[] { "Planned", "Completed", "Cancelled" }.Contains(appointment.Status))
                throw new ArgumentException("Choose a supported appointment kind and status.");
            if (appointment.Start.HasValue != appointment.End.HasValue || appointment.End <= appointment.Start
                || (appointment.Status == "Completed" && !appointment.Start.HasValue))
                throw new ArgumentException("Enter valid start and end times. Completed work needs both times.");
            var all = others.SelectMany(b => b.Appointments).Concat(booking.Appointments.Where(a => a.Id != appointment.Id));
            bool overlaps = appointment.Status != "Cancelled" && appointment.Start.HasValue && all.Any(a =>
                a.Status != "Cancelled" && a.ArtistId == appointment.ArtistId && a.Start < appointment.End && a.End > appointment.Start);
            if (overlaps && string.IsNullOrWhiteSpace(appointment.OverrideReason))
                throw new ArgumentException("This artist has overlapping work. Change the times or record an override reason.");
        }
    }
}
