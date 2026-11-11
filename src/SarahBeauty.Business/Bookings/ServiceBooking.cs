namespace SarahBeauty.Business.Bookings;

internal static class ServiceBooking
{
    internal static Booking Add(Booking booking,ServiceDefinition service,decimal quantity)
    {
        if(service.Archived || service.Kind=="Trial portion")throw new ArgumentException("Choose an active billable service. An included trial portion belongs to the package payment plan.");
        if(quantity<=0 || quantity>100000)throw new ArgumentException("Enter a positive supported service quantity.");
        long price=service.UnitCents??throw new ArgumentException("Confirm this service's price before adding it to a booking.");
        var added=BookingRules.AddCharge(booking,service.Name,service.Unit,quantity,price);
        var line=added.Charges[^1] with{ServiceId=service.Id,ServiceRevision=service.Revision,DurationMinutes=service.DurationMinutes};
        _=line.NetCents;
        return added with{Charges=[..added.Charges[..^1],line]};
    }
}
