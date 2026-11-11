using System.Globalization;

namespace SarahBeauty.Business.Rules;

public static class Money
{
    public static long Parse(string dollars)
    {
        if (!decimal.TryParse(dollars, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out decimal value)) throw new ArgumentException("Enter a USD amount such as 120.00.");
        if (decimal.Round(value, 2) != value) throw new ArgumentException("Enter money with at most two decimal places.");
        return Cents(value);
    }

    public static long Cents(decimal dollars) => checked((long)decimal.Round(dollars * 100m, 0, MidpointRounding.AwayFromZero));
    public static string Display(long cents) => (cents / 100m).ToString("C2", CultureInfo.GetCultureInfo("en-US"));
    public static string Input(long cents) => (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
    public static long Multiply(decimal quantity, long unitCents) => checked((long)decimal.Round(quantity * unitCents, 0, MidpointRounding.AwayFromZero));
    public static void Positive(long cents)
    {
        if (cents <= 0) throw new ArgumentException("The amount must be greater than zero.");
    }

    public static long Proportion(long cents,long part,long whole)
    {
        if(cents<0 || part<0 || whole<=0 || part>whole)throw new ArgumentException("Invalid proportional amounts.");
        System.Numerics.BigInteger numerator=(System.Numerics.BigInteger)cents*part;
        var quotient=System.Numerics.BigInteger.DivRem(numerator,whole,out var remainder);
        return checked((long)(quotient+(remainder*2>=whole?1:0)));
    }
}
