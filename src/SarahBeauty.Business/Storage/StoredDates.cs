using System.Globalization;

namespace SarahBeauty.Business.Storage;

internal static class StoredDates
{
    internal static string ToStorageDate(this DateOnly value)=>value.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
    internal static DateOnly Parse(string value)=>DateOnly.ParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture);
}
