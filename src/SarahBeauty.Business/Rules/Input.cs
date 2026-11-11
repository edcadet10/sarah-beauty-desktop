using System.Globalization;

namespace SarahBeauty.Business.Rules;

public static class Input
{
    public static DateOnly? Date(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new ArgumentException("Enter dates as YYYY-MM-DD, or leave an optional date blank.");
        return date;
    }

    public static decimal Decimal(string value)
    {
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out decimal number)) throw new ArgumentException("Enter a decimal number, such as 12.34.");
        return number;
    }

    public static DateTimeOffset? Time(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!DateTimeOffset.TryParseExact(value.Trim(), "yyyy-MM-dd'T'HH:mmzzz", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var time)) throw new ArgumentException("Enter a time with an explicit offset, such as 2026-11-10T10:00-05:00.");
        return time;
    }
}
