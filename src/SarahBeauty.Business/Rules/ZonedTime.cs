using System.Globalization;

namespace SarahBeauty.Business.Rules;

public static class ZonedTime
{
    public static TimeZoneInfo Zone(string id)
    {
        if(string.IsNullOrWhiteSpace(id))throw new ArgumentException("Choose a known time zone.");
        try{return TimeZoneInfo.FindSystemTimeZoneById(id.Trim());}
        catch(Exception error) when(error is TimeZoneNotFoundException or InvalidTimeZoneException){throw new ArgumentException("Choose an available time zone, such as America/New_York.");}
    }

    public static DateTimeOffset Resolve(DateOnly date,string time,string zoneId,string utcOffset="")
    {
        if(!TimeOnly.TryParseExact(time,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var clock))throw new ArgumentException("Enter the local time as HH:mm.");
        var zone=Zone(zoneId);var local=date.ToDateTime(clock,DateTimeKind.Unspecified);
        var offsets=Offsets(zone,local);
        if(offsets.Length>1 && string.IsNullOrWhiteSpace(utcOffset))throw new ArgumentException("This local time occurs twice. Choose its actual UTC offset explicitly.");
        var selected=string.IsNullOrWhiteSpace(utcOffset)?offsets[0]:ParseOffset(utcOffset);
        if(!offsets.Contains(selected))throw new ArgumentException("The UTC offset does not match this date, local time, and time zone.");
        try{return new(local,selected);}
        catch(ArgumentException){throw new ArgumentException("The local time and UTC offset exceed the supported date range.");}
    }

    public static void Validate(DateTimeOffset value,string zoneId)
    {
        var local=DateTime.SpecifyKind(value.DateTime,DateTimeKind.Unspecified);
        if(!Offsets(Zone(zoneId),local).Contains(value.Offset))throw new ArgumentException("The appointment's UTC offset does not match its stated local time and time zone.");
    }

    internal static TimeSpan ParseOffset(string value)
    {
        value=value.Trim();
        if(value.Length!=6 || value[0] is not ('+' or '-') || !TimeSpan.TryParseExact(value[1..],@"hh\:mm",CultureInfo.InvariantCulture,out var offset)
            || offset>TimeSpan.FromHours(14))throw new ArgumentException("Enter a UTC offset such as -04:00, -05:00, or +05:30.");
        return value[0]=='-'?-offset:offset;
    }

    private static TimeSpan[] Offsets(TimeZoneInfo zone,DateTime local)
    {
        if(zone.IsInvalidTime(local))throw new ArgumentException("This local time does not exist during the daylight-saving clock change. Choose an actual local time.");
        return zone.IsAmbiguousTime(local)?zone.GetAmbiguousTimeOffsets(local):[zone.GetUtcOffset(local)];
    }
}
