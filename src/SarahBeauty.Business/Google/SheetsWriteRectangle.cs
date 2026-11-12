using System.Globalization;

namespace SarahBeauty.Business.Google;

internal sealed record SheetsWriteRectangle(string Table,int StartRow,int StartColumn,IReadOnlyList<IReadOnlyList<string>> Values)
{
    internal string Range=>$"'{Table}'!{Column(StartColumn)}{(StartRow+1).ToString(CultureInfo.InvariantCulture)}:{Column(StartColumn+Values[0].Count-1)}{(StartRow+Values.Count).ToString(CultureInfo.InvariantCulture)}";
    private static string Column(int zeroBased)
    {
        if(zeroBased is <0 or >127)throw new ArgumentOutOfRangeException(nameof(zeroBased));string text="";
        for(int value=zeroBased+1;value>0;value=(value-1)/26)text=(char)('A'+(value-1)%26)+text;
        return text;
    }
}
