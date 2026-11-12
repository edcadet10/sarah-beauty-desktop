using System.Globalization;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Exports;

internal static class ReadableScheduleTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read(db=>
    {
        var artists=Artists(db,snapshot.Source,budget);var bookingWork=BookingWork(db,snapshot.Source,budget);return new[]{artists}.Concat(bookingWork).ToArray();
    });
    private static ReadableExportTable Artists(DbWork db,ReadableExportSource source,ReadableExportBudget budget)
    {
        var table=new ReadableTableBuilder(source,budget,"Artists","ArtistId","BusinessId","Revision","Name","Archived","CreatedAtUtc","CreatedByOwnerId","UpdatedAtUtc","UpdatedByOwnerId");ReadableRecords.ForEach<Artist>(db,source,"Artist",(artist,audit)=>table.Add(artist.Id,source.BusinessId,artist.Revision,artist.Name,artist.Archived,audit.CreatedAtUtc,audit.CreatedByOwnerId,audit.UpdatedAtUtc,audit.UpdatedByOwnerId));return table.Freeze();
    }
    private static ReadableExportTable[] BookingWork(DbWork db,ReadableExportSource source,ReadableExportBudget budget)
    {
        var appointments=new ReadableTableBuilder(source,budget,"Appointments","AppointmentId","BookingId","BusinessId","BookingStatus","BookingArchived","Kind","ArtistId","Status","StartUtc","EndUtc","RecordedStartWithOffset","RecordedEndWithOffset","TimeZoneId","Location");
        var places=new ReadableTableBuilder(source,budget,"Appointment places","AppointmentId","BookingId","BusinessId","PlaceId");
        var status=new ReadableTableBuilder(source,budget,"Booking status history","BookingId","BusinessId","Sequence","From","To","RecordedByOwnerId","AtUtc");
        var reservations=new ReadableTableBuilder(source,budget,"Booking reservations","RequestId","BookingId","BusinessId","QuoteVersionId","Date","TrialCents","DepositCents","RecordedByOwnerId","AtUtc");
        ReadableRecords.ForEach<Booking>(db,source,"Booking",(booking,_)=>
        {
            foreach(var work in booking.Appointments.OrderBy(a=>a.Id))
            {
                appointments.Add(work.Id,booking.Id,source.BusinessId,booking.Status,booking.Archived,work.Kind,work.ArtistId,work.Status,work.Start,work.End,work.Start?.ToString("O",CultureInfo.InvariantCulture),work.End?.ToString("O",CultureInfo.InvariantCulture),work.TimeZoneId,work.Location);
                foreach(string place in work.PlaceIds.Order(StringComparer.Ordinal))places.Add(work.Id,booking.Id,source.BusinessId,place);
            }
            for(int i=0;i<booking.History.Length;i++){var change=booking.History[i];status.Add(booking.Id,source.BusinessId,i+1,change.From,change.To,change.OwnerId,change.AtUtc);}
            foreach(var decision in booking.Reservations.OrderBy(r=>r.AtUtc,StringComparer.Ordinal).ThenBy(r=>r.RequestId,StringComparer.Ordinal))reservations.Add(decision.RequestId,booking.Id,source.BusinessId,decision.QuoteVersionId,decision.Date,decision.TrialCents,decision.DepositCents,decision.OwnerId,decision.AtUtc);
        });return [appointments.Freeze(),places.Freeze(),status.Freeze(),reservations.Freeze()];
    }
}
