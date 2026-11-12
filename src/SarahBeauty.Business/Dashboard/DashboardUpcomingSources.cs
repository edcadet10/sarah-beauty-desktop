using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Dashboard;

public sealed class DashboardUpcomingSources(BusinessStore store)
{
    internal Action? AfterBookingsRead {get;set;}
    public DashboardUpcomingSnapshot Load(OwnerSession owner,DateOnly asOf,DateOnly from,DateOnly through)=>store.ReadSnapshot(db=>Load(db,owner,asOf,from,through));

    internal DashboardUpcomingSnapshot Load(DbWork db,OwnerSession owner,DateOnly asOf,DateOnly from,DateOnly through)
    {
        store.RequireSession(db,owner);
        var result=Read(db,owner.BusinessId,asOf,from,through,DateTimeOffset.UtcNow.ToString("O"),AfterBookingsRead);
        store.RequireSession(db,owner);
        return result;
    }

    internal static DashboardUpcomingSnapshot Read(DbWork db,string businessId,DateOnly asOf,DateOnly from,DateOnly through,string generatedAtUtc,Action? afterBookingsRead=null)
    {
        int count=(through.Year-from.Year)*12+through.Month-from.Month+1;
        if(from<asOf || through<from || count>120)throw new ArgumentException("Choose a confirmed-event window starting on or after its planning as-of date and spanning at most 120 calendar months.");
        var candidates=RecordStore<Booking>.ReadBusiness(db,businessId,"Booking").Where(b=>!b.Archived && b.Status=="Confirmed" && b.EventType=="Wedding"
            && (b.EventDate is null || b.EventDate>=from && b.EventDate<=through)).ToArray();afterBookingsRead?.Invoke();
        var documents=DocumentVersions.List(db,businessId);var artists=RecordStore<Artist>.ReadBusiness(db,businessId,"Artist").ToDictionary(a=>a.Id);
        var rows=candidates.Select(b=>Position(db,businessId,b,documents,artists,BookingBillingSources.Read(db,businessId,b,asOf,documents,generatedAtUtc),asOf)).ToArray();
        var dated=rows.Where(b=>b.EventDate.HasValue).OrderBy(b=>b.EventDate).ThenBy(b=>b.Name).ToArray();
        var groups=dated.ToLookup(b=>new DateOnly(b.EventDate!.Value.Year,b.EventDate.Value.Month,1));DateOnly first=new(from.Year,from.Month,1);
        var months=Enumerable.Range(0,count).Select(i=>Month(first.AddMonths(i),groups)).ToArray();
        return new(db.Scalar("SELECT DisplayName FROM Business WHERE Id=$id;",("$id",businessId)) as string??throw new ArgumentException("Choose a business present in the planning source."),
            (string)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!,Convert.ToInt64(db.Scalar("SELECT Watermark FROM Business WHERE Id=$id;",("$id",businessId))),generatedAtUtc,asOf,from,through,
            dated,rows.Where(b=>b.EventDate is null).ToArray(),months);
    }

    internal static DashboardUpcomingBooking Position(DbWork db,OwnerSession owner,Booking booking,List<DocumentVersion> documents,IReadOnlyDictionary<string,Artist> artists,
        BookingBillingSnapshot billing,DateOnly asOf)=>Position(db,owner.BusinessId,booking,documents,artists,billing,asOf);
    internal static DashboardUpcomingBooking Position(DbWork db,string businessId,Booking booking,List<DocumentVersion> documents,IReadOnlyDictionary<string,Artist> artists,
        BookingBillingSnapshot billing,DateOnly asOf)
    {
        var quote=documents.SingleOrDefault(d=>d.Id==booking.AcceptedQuoteVersionId && d.Snapshot.Kind==DocumentKind.Quote);
        var issues=new List<string>();long? amount=null;
        if(quote is null || quote.Status!="Accepted" || quote.AcceptedDate is null || quote.AcceptedDate>asOf)
            issues.Add("The current agreement has no confirmed acceptance on or before the planning as-of date.");
        else
        {
            amount=DashboardAgreementValue.Read(quote,billing,issues);
            DashboardReservationReview.Check(db,businessId,booking,quote,asOf,issues);
        }
        var staff=booking.Appointments.Where(a=>a.Status!="Cancelled" && a.Kind=="Wedding").Select(a=>a.ArtistId).Distinct().Select(id=>
            artists.TryGetValue(id,out var artist)?new DashboardUpcomingArtist(id,artist.Name,artist.Archived?"Archived":"Active"):new(id,"Artist unavailable","Unavailable")).ToArray();
        if(staff.Any(a=>a.State!="Active"))issues.Add("An event artist is archived or unavailable. Review the current assignment.");
        return new(booking.Id,booking.Name,booking.EventDate,booking.Venue,booking.ReadyBy,booking.EventTimeZoneId,staff,
            quote?.Id??"",quote is null?"Unconfirmed agreement":$"{quote.Snapshot.Number} v{quote.Snapshot.Version}",quote?.Hash??"",amount,issues.Distinct().ToArray(),billing);
    }

    private static DashboardUpcomingMonth Month(DateOnly month,ILookup<DateOnly,DashboardUpcomingBooking> groups)
    {
        var rows=groups[month].ToArray();return new(month,rows.Length,rows.Where(r=>r.AgreementGrossCents.HasValue).Sum(r=>r.AgreementGrossCents!.Value),
            rows.Count(r=>!r.AgreementGrossCents.HasValue),rows.Select(r=>r.BookingId).ToArray());
    }
}
