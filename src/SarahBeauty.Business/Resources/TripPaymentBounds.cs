using SarahBeauty.Business.Finance;

namespace SarahBeauty.Business.Resources;

internal static class TripPaymentBounds
{
    internal static void Validate(TripPaymentAllocation[] links,TripPaymentRelease[] releases,TripPaymentSource[] payments)
    {
        Distinct(links.Select(l=>l.Id));Distinct(releases.Select(r=>r.Id));Distinct(payments.Select(p=>p.Id));
        foreach(var link in links)Link(link,payments);
        foreach(var release in releases)
        {
            var link=links.SingleOrDefault(l=>l.Id==release.AllocationId)??throw new ArgumentException("Choose an original trip payment link.");
            if(release.Cents<=0 || release.Date<link.Date)throw new ArgumentException("A positive release cannot precede its original trip payment link.");
        }
        foreach(var group in links.GroupBy(l=>l.PaymentId))Payment(payments.Single(p=>p.Id==group.Key),group.ToArray(),releases);
        foreach(var group in links.GroupBy(l=>l.TripId))Trip(group.ToArray(),releases);
    }

    private static void Distinct(IEnumerable<string> source)
    {
        var ids=source.ToArray();if(ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct().Count()!=ids.Length)throw new ArgumentException("Retain distinct permanent trip payment record identities.");
    }

    private static void Link(TripPaymentAllocation link,TripPaymentSource[] payments)
    {
        var payment=payments.SingleOrDefault(p=>p.Id==link.PaymentId)??throw new ArgumentException("Choose an actual approved trip payment.");
        var basis=link.Basis;
        if(payment.Kind is not (FinancialKind.Expense or FinancialKind.OwnerReimbursement) || payment.Cents<=0 || payment.CashAccountId.Length==0
            || link.Cents<=0 || basis.RequestedCents<=0 || link.Date<payment.Date || link.Date<basis.TripDate || link.TripId.Length==0 || basis.Hash.Length==0)
            throw new ArgumentException("Link a positive reviewed claim to actual paid expense or owner reimbursement on or after the trip and payment dates.");
        if((basis.DriverOwnerId.Length>0)==(basis.DriverArtistId.Length>0))throw new ArgumentException("Retain exactly one actual trip driver.");
        if(payment.Kind==FinancialKind.OwnerReimbursement && (basis.DriverOwnerId.Length==0 || payment.OwnerId!=basis.DriverOwnerId))
            throw new ArgumentException("The reimbursement payment must name this trip's actual owner driver.");
    }

    private static IEnumerable<DateOnly> Dates(TripPaymentAllocation[] links,TripPaymentRelease[] releases)=>
        links.Select(l=>l.Date).Concat(releases.Where(r=>links.Any(l=>l.Id==r.AllocationId)).Select(r=>r.Date)).Distinct().Order();

    private static void Payment(TripPaymentSource payment,TripPaymentAllocation[] links,TripPaymentRelease[] releases)
    {
        foreach(var date in Dates(links,releases).Concat(payment.Adjustments.Select(a=>a.Date)).Distinct())
        {
            foreach(var link in links)
                if(TripPaymentAmounts.Remaining(link,releases,date)<0)throw new ArgumentException("Trip payment releases exceed their original linked amount.");
            if(TripPaymentAmounts.Unlinked(payment,links,releases,date)<0)
                throw new ArgumentException($"Trip reimbursement links exceed the actual payment available on {date:yyyy-MM-dd}. Release affected links before cancelling or returning the source payment.");
        }
    }

    private static void Trip(TripPaymentAllocation[] links,TripPaymentRelease[] releases)
    {
        foreach(var basis in links.GroupBy(l=>l.Basis.Hash))
            if(basis.Select(l=>l.Basis).Distinct().Count()!=1)throw new ArgumentException("A retained trip claim identity cannot change its driver, date, or requested amount.");
        foreach(var date in Dates(links,releases))
        {
            var active=links.Where(l=>TripPaymentAmounts.Remaining(l,releases,date)>0).ToArray();
            if(active.Select(l=>l.Basis.Hash).Distinct().Count()>1)throw new ArgumentException("Release the earlier trip claim links before linking a changed claim basis.");
            if(active.Length>0 && TripPaymentAmounts.Paid(active,releases,date)>active[0].Basis.RequestedCents)
                throw new ArgumentException($"Linked payments exceed the reviewed reimbursement request on {date:yyyy-MM-dd}.");
        }
    }
}
