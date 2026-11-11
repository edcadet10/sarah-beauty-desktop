using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class BookingReportFactsReader
{
    internal static BookingReportFacts Read(DbWork db,OwnerSession owner,Booking booking,IReadOnlyDictionary<string,string> artists)
    {
        string Name(string id)=>id.Length==0?"Unnamed recipient":CustomerBook.Find(db,owner,id)?.FullName??"Customer unavailable";
        var charges=booking.Charges.Select(c=>new BookingReportCharge(c.Id,c.Description,c.Unit,c.Quantity,c.UnitCents,c.DiscountCents,c.NetCents,c.TaxRate,c.Active,c.DurationMinutes)).ToArray();
        var places=booking.Places.Select(p=>new BookingReportPlace(p.Id,p.ChargeId,p.Ordinal,p.CustomerId,Name(p.CustomerId),p.Active)).ToArray();
        var appointments=booking.Appointments.Select(a=>new BookingReportAppointment(a.Id,a.Kind,a.ArtistId,artists.GetValueOrDefault(a.ArtistId,"Artist unavailable"),
            a.Start,a.End,a.TimeZoneId,a.Location,a.Status,a.PlaceIds)).ToArray();
        return new(booking.Id,booking.Name,booking.Revision,booking.Status,booking.Archived,
            booking.PrimaryCustomerId.Length==0?"Undecided":Name(booking.PrimaryCustomerId),QuoteSnapshot.Event(booking),booking.HasTrial,charges,places,appointments);
    }
}
