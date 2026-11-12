using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class BookingContributionReports
{
    internal static BookingReportSnapshot Read(DbWork db,BusinessStore store,OwnerSession owner,DateOnly from,DateOnly through,List<DocumentVersion> versions)
    {
        store.RequireSession(db,owner);
        return Read(db,owner.BusinessId,from,through,versions,DateTimeOffset.UtcNow.ToString("O"));
    }

    internal static BookingReportSnapshot Read(DbWork db,string businessId,DateOnly from,DateOnly through,List<DocumentVersion> versions,string generatedAtUtc)
    {
        if(from>through)throw new ArgumentException("The booking financial period ends before it starts.");
        var bookings=RecordStore<Booking>.ReadBusiness(db,businessId,"Booking");
        var artists=RecordStore<Artist>.ReadBusiness(db,businessId,"Artist").ToDictionary(a=>a.Id,a=>a.Name);
        var positions=bookings.Select(b=>new BookingReportPosition(BookingReportFactsReader.Read(db,businessId,b,artists),
            BookingBillingSources.Read(db,businessId,b,through,versions,generatedAtUtc),BookingFinancialSources.Read(db,businessId,b,from,through,generatedAtUtc))).ToList();
        return new(1,positions,BookingFinancialSources.UnassignedCosts(db,businessId,through));
    }
}
