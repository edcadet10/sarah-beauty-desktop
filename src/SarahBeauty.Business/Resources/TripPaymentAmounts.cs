using System.Numerics;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Business.Resources;

internal sealed record TripPaymentAdjustment(string Id,FinancialKind Kind,DateOnly Date,long Cents,string OriginalId="",string ReversesId="");
internal sealed record TripPaymentSource(string Id,FinancialKind Kind,DateOnly Date,long Cents,string CashAccountId,string OwnerId,TripPaymentAdjustment[] Adjustments);
internal sealed record TripPaymentBasis(string Hash,DateOnly TripDate,long RequestedCents,string DriverOwnerId,string DriverArtistId);
internal sealed record TripPaymentAllocation(string Id,string TripId,TripPaymentBasis Basis,string PaymentId,DateOnly Date,long Cents);
internal sealed record TripPaymentRelease(string Id,string AllocationId,DateOnly Date,long Cents);

internal static class TripPaymentAmounts
{
    internal static BigInteger Remaining(TripPaymentAllocation link,IEnumerable<TripPaymentRelease> releases,DateOnly date)=>link.Date>date?0:
        new BigInteger(link.Cents)-releases.Where(r=>r.AllocationId==link.Id && r.Date<=date).Aggregate(BigInteger.Zero,(sum,r)=>sum+r.Cents);

    internal static BigInteger Paid(IEnumerable<TripPaymentAllocation> links,IEnumerable<TripPaymentRelease> releases,DateOnly date)=>
        links.Aggregate(BigInteger.Zero,(sum,l)=>sum+Remaining(l,releases,date));

    internal static BigInteger ActualPayment(TripPaymentSource source,DateOnly date)
    {
        if(source.Date>date || source.Adjustments.Any(a=>a.Kind==FinancialKind.Reversal && a.ReversesId==source.Id && a.Date<=date))return 0;
        var returned=source.Adjustments.Where(a=>a.Kind==FinancialKind.SupplierRefund && a.OriginalId==source.Id && a.Date<=date
            && !source.Adjustments.Any(r=>r.Kind==FinancialKind.Reversal && r.ReversesId==a.Id && r.Date<=date));
        return new BigInteger(source.Cents)-returned.Aggregate(BigInteger.Zero,(sum,a)=>sum+a.Cents);
    }

    internal static BigInteger Unlinked(TripPaymentSource source,IEnumerable<TripPaymentAllocation> links,IEnumerable<TripPaymentRelease> releases,DateOnly date)=>
        ActualPayment(source,date)-Paid(links.Where(l=>l.PaymentId==source.Id),releases,date);
}
