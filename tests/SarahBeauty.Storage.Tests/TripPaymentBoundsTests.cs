using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class TripPaymentBoundsTests
{
    private static readonly DateOnly Day=new(2027,1,10);
    private static TripPaymentSource Payment(string id="payment",long cents=10000)=>new(id,FinancialKind.Expense,Day,cents,"fictional-bank","",[]);
    private static TripPaymentAllocation Link(string id="link",string trip="trip",string payment="payment",long cents=5000,long claim=10000,int day=1,string basis="basis")=>
        new(id,trip,new(basis,Day,claim,"fictional-owner",""),payment,Day.AddDays(day),cents);
    private static void Check(TripPaymentAllocation[] links,TripPaymentRelease[]? releases=null,TripPaymentSource[]? payments=null)=>TripPaymentBounds.Validate(links,releases??[],payments??[Payment()]);

    [Fact]
    public void PartialAndCombinedLinksRespectBothPaymentsAndReviewedClaims()
    {
        var links=new[]{Link("one","trip-one",cents:4000,claim:6000),Link("two","trip-two",cents:4000,claim:4000,basis:"second-basis"),Link("three","trip-one","second-payment",2000,6000)};
        var payments=new[]{Payment(),Payment("second-payment",2000)};Check(links,payments:payments);
        Assert.Equal(6000,(long)TripPaymentAmounts.Paid(links.Where(l=>l.TripId=="trip-one"),[],Day.AddDays(1)));
        Assert.Equal(2000,(long)TripPaymentAmounts.Unlinked(payments[0],links,[],Day.AddDays(1)));
        Assert.Equal(0,(long)TripPaymentAmounts.Unlinked(payments[1],links,[],Day.AddDays(1)));
        Assert.Equal(0,(long)TripPaymentAmounts.Paid(links,[],Day));
    }

    [Fact]
    public void ACombinedExcessCannotHideBehindIndividuallyValidRows()
    {
        Assert.Throws<ArgumentException>(()=>Check([Link("one",cents:6000,claim:9000),Link("two",cents:4000,claim:9000)]));
        Assert.Throws<ArgumentException>(()=>Check([Link("one","trip-one",cents:6000),Link("two","trip-two",cents:6000)]));
        Assert.Throws<ArgumentException>(()=>Check([Link()],releases:[new("one","link",Day.AddDays(2),3000),new("two","link",Day.AddDays(3),3000)]));
    }

    [Fact]
    public void LaterReturnsAndSourceCancellationNeedDatedReleasesRatherThanCurrentNetting()
    {
        var link=Link(cents:8000);var source=Payment() with{Adjustments=[new("returned",FinancialKind.SupplierRefund,Day.AddDays(3),3000,OriginalId:"payment")]};
        Assert.Throws<ArgumentException>(()=>Check([link],payments:[source]));
        TripPaymentRelease[] releases=[new("released","link",Day.AddDays(3),1000)];Check([link],releases,[source]);
        source=source with{Adjustments=[..source.Adjustments,new("return-reversed",FinancialKind.Reversal,Day.AddDays(5),3000,ReversesId:"returned")]};
        Assert.Equal(3000,(long)TripPaymentAmounts.Unlinked(source,[link],releases,Day.AddDays(5)));
        source=source with{Adjustments=[..source.Adjustments,new("cancelled",FinancialKind.Reversal,Day.AddDays(6),10000,ReversesId:"payment")]};
        Assert.Throws<ArgumentException>(()=>Check([link],releases,[source]));
        Check([link],[..releases,new("final-release","link",Day.AddDays(6),7000)],[source]);
        Assert.Throws<ArgumentException>(()=>Check([link],[..releases,new("late-release","link",Day.AddDays(7),7000)],[source]));
    }

    [Fact]
    public void AChangedClaimCannotOverlapItsEarlierRetainedBasisOrRewriteItsCap()
    {
        var old=Link("old",cents:6000);var revised=Link("new",cents:4000,claim:4000,day:3,basis:"revised");
        Assert.Throws<ArgumentException>(()=>Check([old,revised]));
        Check([old,revised],[new("released","old",Day.AddDays(2),6000)]);
        Assert.Throws<ArgumentException>(()=>Check([old,revised],[new("partial","old",Day.AddDays(2),3000)]));
        Assert.Throws<ArgumentException>(()=>Check([old,revised with{Basis=revised.Basis with{Hash=old.Basis.Hash}}],[new("released","old",Day.AddDays(2),6000)]));
    }

    [Fact]
    public void DatesCashKindsAndOwnerDriverIdentityAreExplicit()
    {
        var link=Link();var reimbursement=Payment() with{Kind=FinancialKind.OwnerReimbursement,OwnerId="fictional-owner"};Check([link],payments:[reimbursement]);
        Assert.Throws<ArgumentException>(()=>Check([link],payments:[reimbursement with{OwnerId="different-owner"}]));
        var artist=link with{Basis=link.Basis with{DriverOwnerId="",DriverArtistId="fictional-artist"}};Check([artist]);
        Assert.Throws<ArgumentException>(()=>Check([artist],payments:[reimbursement]));
        Assert.Throws<ArgumentException>(()=>Check([link],payments:[Payment() with{Kind=FinancialKind.OwnerPaidExpense}]));
        Assert.Throws<ArgumentException>(()=>Check([link],payments:[Payment() with{CashAccountId=""}]));
        Assert.Throws<ArgumentException>(()=>Check([link with{Date=Day.AddDays(-1)}]));
        Assert.Throws<ArgumentException>(()=>Check([link with{Basis=link.Basis with{RequestedCents=0}}]));
        Assert.Throws<ArgumentException>(()=>Check([link],[new("early","link",Day,1)]));
    }

    [Fact]
    public void MaximumCentAmountsRejectAggregateExcessWithoutIntegerOverflow()
    {
        var maximum=Link("maximum",cents:long.MaxValue,claim:long.MaxValue);var extra=Link("extra",payment:"second",cents:1,claim:long.MaxValue);
        var payments=new[]{Payment(cents:long.MaxValue),Payment("second",1)};Check([maximum],payments:payments);
        Assert.Throws<ArgumentException>(()=>Check([maximum,extra],payments:payments));
        TripPaymentRelease[] released=[new("released","maximum",Day.AddDays(1),long.MaxValue-1)];Check([maximum,extra],released,payments);
        Assert.Equal(2,(long)TripPaymentAmounts.Paid([maximum,extra],released,Day.AddDays(1)));
    }

    [Fact]
    public void SourceAndReleaseIdentitiesCannotBeInventedOrDuplicated()
    {
        Assert.Throws<ArgumentException>(()=>Check([Link(),Link()]));
        Assert.Throws<ArgumentException>(()=>Check([Link(payment:"foreign-payment")]));
        Assert.Throws<ArgumentException>(()=>Check([Link()],[new("release","foreign-link",Day.AddDays(1),1)]));
        Assert.Throws<ArgumentException>(()=>Check([Link()],[new("release","link",Day.AddDays(1),1),new("release","link",Day.AddDays(2),1)]));
    }
}
