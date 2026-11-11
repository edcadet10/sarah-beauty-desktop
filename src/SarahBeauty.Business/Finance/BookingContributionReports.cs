using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class BookingContributionReports
{
    internal static BookingReportSnapshot Read(DbWork db,BusinessStore store,OwnerSession owner,DateOnly from,DateOnly through,List<DocumentVersion> versions)
    {
        var bookings=new RecordStore<Booking>(store,"Booking").List(db,owner);
        var artists=new RecordStore<Artist>(store,"Artist").List(db,owner).ToDictionary(a=>a.Id,a=>a.Name);
        var billing=new BookingBillingSources(store);var financial=new BookingFinancialSources(store);
        var positions=bookings.Select(b=>new BookingReportPosition(BookingReportFactsReader.Read(db,owner,b,artists),
            billing.Load(db,owner,b.Id,through,versions),financial.Load(db,owner,b.Id,from,through))).ToList();
        return new(1,positions,BookingFinancialSources.UnassignedCosts(db,owner,through));
    }
}
