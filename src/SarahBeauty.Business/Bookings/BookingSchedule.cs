using System.Globalization;
using SarahBeauty.Business.Rules;

namespace SarahBeauty.Business.Bookings;

internal static class BookingSchedule
{
    internal static void Validate(Booking booking)
    {
        _=ZonedTime.Zone(booking.EventTimeZoneId);
        if(string.IsNullOrWhiteSpace(booking.ReadyBy))
        {
            if(!string.IsNullOrWhiteSpace(booking.ReadyByUtcOffset))throw new ArgumentException("Enter the ready-by time before its optional UTC offset.");
            return;
        }
        if(!TimeOnly.TryParseExact(booking.ReadyBy,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))throw new ArgumentException("Enter the ready-by local time as HH:mm.");
        if(!string.IsNullOrWhiteSpace(booking.ReadyByUtcOffset))_=ZonedTime.ParseOffset(booking.ReadyByUtcOffset);
        _=ReadyBy(booking);
    }

    internal static DateTimeOffset? ReadyBy(Booking booking)=>booking.EventDate.HasValue && !string.IsNullOrWhiteSpace(booking.ReadyBy)
        ?ZonedTime.Resolve(booking.EventDate.Value,booking.ReadyBy,booking.EventTimeZoneId,booking.ReadyByUtcOffset):null;
}
