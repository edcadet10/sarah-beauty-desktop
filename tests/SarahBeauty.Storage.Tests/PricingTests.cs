using SarahBeauty.Business.Rules;

namespace SarahBeauty.Storage.Tests;

public sealed class PricingTests
{
    [Theory]
    [InlineData(30000, 9, 12000, "76.6", 100, 145660, 65330, 65330)]
    [InlineData(30001, 9, 12000, "76.6", 100, 145661, 65331, 65330)]
    [InlineData(32501, 3, 13749, "12.34", 125, 75291, 30146, 30145)]
    public void WeddingFixturesRoundExactly(long bridal, int guests, long guestPrice, string miles,
        long travelRate, long expected, long deposit, long final)
    {
        long total = bridal + guests * guestPrice + Money.Multiply(decimal.Parse(miles,
            System.Globalization.CultureInfo.InvariantCulture), travelRate);
        var plan = PaymentPlan.Bridal(total, 15000);
        Assert.Equal(expected, total);
        Assert.Equal(deposit, plan.DepositCents);
        Assert.Equal(final, plan.FinalCents);
        Assert.Equal(total, plan.TotalCents);
    }

    [Fact]
    public void CentAllocationPreservesTotalsAndSkipsZeroWeights()
    {
        long[] shares = PaymentPlan.Allocate(145661, [3, 0, 2, 1]);
        Assert.Equal(145661, shares.Sum());
        Assert.Equal(0, shares[1]);
        Assert.Equal(shares, PaymentPlan.Allocate(145661, [3, 0, 2, 1]));
        Assert.Equal(long.MaxValue, PaymentPlan.Allocate(long.MaxValue, [long.MaxValue - 1, 1]).Sum());
        Assert.Throws<OverflowException>(() => PaymentPlan.Allocate(1, [long.MaxValue, 1]));
    }

    [Theory]
    [InlineData("0.001")]
    [InlineData("not money")]
    [InlineData("1e10")]
    public void InvalidMoneyIsRejected(string text) => Assert.Throws<ArgumentException>(() => Money.Parse(text));

    [Fact]
    public void NonBridalPlanDoesNotAddATrial()
    {
        Assert.Equal(new PaymentPlan(0, 12000, 24000), PaymentPlan.Custom(36000, 12000));
        Assert.Throws<ArgumentException>(() => PaymentPlan.Bridal(10000, 15000));
        Assert.Throws<OverflowException>(() => Money.Multiply(decimal.MaxValue, long.MaxValue));
    }

    [Theory]
    [InlineData(1L,1L,2L,1L)]
    [InlineData(100L,1L,3L,33L)]
    [InlineData(long.MaxValue,long.MaxValue,long.MaxValue,long.MaxValue)]
    public void MonetaryProportionsRoundExactlyWithoutIntermediateOverflow(long cents,long part,long whole,long expected)=>
        Assert.Equal(expected,Money.Proportion(cents,part,whole));
}
