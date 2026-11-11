using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Dashboard;

internal static class DashboardBookingWork
{
    internal static IEnumerable<DashboardWorkItem> Read(DbWork db,OwnerSession owner,DateOnly asOf,Booking[] bookings,List<DocumentVersion> documents,
        IReadOnlyDictionary<string,Artist> artists,IReadOnlyDictionary<string,BookingBillingSnapshot> billing)
    {
        var items=new List<DashboardWorkItem>();
        foreach(var booking in bookings)
        {
            var sources=billing[booking.Id];
            foreach(var group in sources.Dues.Where(d=>d.Cents>0 && d.DueDate is null).GroupBy(d=>d.InvoiceVersionId))
                items.Add(new("Invoice due date",group.Key,group.First().Number+" • "+group.First().CustomerName,"Invoices",0,["Remaining invoice shares have no recorded due date."],group.Sum(d=>d.Cents)));
            if(booking.Archived || booking.Status is "Cancelled" or "Completed")continue;
            var reasons=new List<string>();
            if(booking.Charges.Any(c=>c.Active && c.TaxRate is null))reasons.Add("Active charge tax treatment unresolved.");
            if(booking.AcceptedQuoteVersionId.Length>0 && booking.Status is "Awaiting deposit" or "Confirmed")
                reasons.AddRange(DashboardUpcomingSources.Position(db,owner,booking,documents,artists,sources,asOf).ReviewIssues);
            if(reasons.Count>0)items.Add(new("Booking review",booking.Id,booking.Name,"Bookings",booking.Revision,reasons.Distinct().ToArray()));
            foreach(var quote in documents.Where(v=>v.BookingId==booking.Id && v.Snapshot.Kind==DocumentKind.Quote && v.Status=="Issued" && v.Snapshot.IssueDate<=asOf))
                items.Add(new("Quote acceptance",quote.Id,quote.Snapshot.Number+" • "+booking.Name,"Quotes",0,["Acceptance of this issued proposal has not been recorded."]));
        }
        return items;
    }
}
