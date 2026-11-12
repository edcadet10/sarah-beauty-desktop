using System.Globalization;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Exports;

internal static class ReadableBookingPlanningTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;var tables=new Tables(source,budget);
        var artists=RecordStore<Artist>.ReadBusiness(db,source.BusinessId,"Artist").ToDictionary(a=>a.Id,a=>a.Name);
        tables.Coverage(source.GenerationId,"Captured source",1);
        foreach(var booking in RecordStore<Booking>.ReadBusiness(db,source.BusinessId,"Booking").OrderBy(b=>b.Id))tables.Add(source.GenerationId,BookingReportFactsReader.Read(db,source.BusinessId,booking,artists));
        ReadableBookHistory.ForEachClosed(db,source,(id,period)=>
        {
            if(period.Reports.Bookings is not {} bookings){tables.Coverage(id,"Not retained",null);return;}
            if(bookings.Version!=1)throw new InvalidDataException("The retained booking report version is unsupported.");
            tables.Coverage(id,"Retained at close",bookings.Version);
            foreach(var row in bookings.Positions.OrderBy(p=>p.CurrentFacts.BookingId))tables.Add(id,row.CurrentFacts);
        });
        return tables.Freeze();
    });

    private sealed class Tables
    {
        private readonly ReadableExportSource _source;
        private readonly ReadableTableBuilder _bookings,_charges,_places,_appointments,_recipients,_coverage;
        internal Tables(ReadableExportSource source,ReadableExportBudget budget)
        {
            _source=source;
            _bookings=new(source,budget,"Report booking planning facts","ReportId","BusinessId","BookingId","NameAtCapture","RevisionAtCapture","StatusAtCapture","ArchivedAtCapture","PrimaryCustomerNameAtCapture","EventName","EventDate","Venue","ReadyByLocalTime","EventTimeZoneId","ReadyByUtcOffset","HasTrialAtCapture");
            _charges=new(source,budget,"Report booking planned charges","ReportId","BusinessId","BookingId","ChargeId","Description","Unit","Quantity","UnitCents","DiscountCents","NetCents","TaxRateFraction","ActiveAtCapture","DurationMinutes");
            _places=new(source,budget,"Report booking recipient places","ReportId","BusinessId","BookingId","PlaceId","ChargeId","Ordinal","CustomerId","NameAtCapture","ActiveAtCapture");
            _appointments=new(source,budget,"Report booking appointments","ReportId","BusinessId","BookingId","AppointmentId","Kind","ArtistId","ArtistNameAtCapture","StartUtc","EndUtc","RecordedStartOffset","RecordedEndOffset","TimeZoneId","Location","StatusAtCapture");
            _recipients=new(source,budget,"Report booking appointment places","ReportId","BusinessId","BookingId","AppointmentId","PlaceId");
            _coverage=new(source,budget,"Booking planning report coverage","ReportId","BusinessId","PlanningFacts","SnapshotVersion");
        }
        internal void Coverage(string id,string kind,int? version)=>_coverage.Add(id,_source.BusinessId,kind,version);
        internal void Add(string id,BookingReportFacts facts)
        {
            var value=facts.Event;
            _bookings.Add(id,_source.BusinessId,facts.BookingId,facts.Name,facts.Revision,facts.Status,facts.Archived,facts.CurrentPrimaryCustomerName,value.Name,value.Date,value.Venue,value.ReadyBy,value.TimeZoneId,value.ReadyByUtcOffset,facts.HasTrial);
            foreach(var row in facts.Charges.OrderBy(c=>c.Id))_charges.Add(id,_source.BusinessId,facts.BookingId,row.Id,row.Description,row.Unit,row.Quantity,row.UnitCents,row.DiscountCents,row.NetCents,row.TaxRate,row.Active,row.DurationMinutes);
            foreach(var row in facts.Places.OrderBy(p=>p.Id))_places.Add(id,_source.BusinessId,facts.BookingId,row.Id,row.ChargeId,row.Ordinal,row.CustomerId,row.CurrentName,row.Active);
            foreach(var row in facts.Appointments.OrderBy(a=>a.Id))
            {
                _appointments.Add(id,_source.BusinessId,facts.BookingId,row.Id,row.Kind,row.ArtistId,row.CurrentArtistName,row.Start,row.End,row.Start?.Offset.ToString("c",CultureInfo.InvariantCulture),row.End?.Offset.ToString("c",CultureInfo.InvariantCulture),row.TimeZoneId,row.Location,row.Status);
                foreach(string place in row.PlaceIds.Order())_recipients.Add(id,_source.BusinessId,facts.BookingId,row.Id,place);
            }
        }
        internal ReadableExportTable[] Freeze()=>[_bookings.Freeze(),_charges.Freeze(),_places.Freeze(),_appointments.Freeze(),_recipients.Freeze(),_coverage.Freeze()];
    }
}
