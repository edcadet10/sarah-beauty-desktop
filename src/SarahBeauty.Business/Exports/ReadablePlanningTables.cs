using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Exports;

internal static class ReadablePlanningTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read(db=>
    {
        var catalog=Catalog(db,snapshot.Source,budget);var bookings=Bookings(db,snapshot.Source,budget);return catalog.Concat(bookings).ToArray();
    });
    private static ReadableExportTable[] Catalog(DbWork db,ReadableExportSource source,ReadableExportBudget budget)
    {
        var current=new ReadableTableBuilder(source,budget,"Service catalog","ServiceId","BusinessId","Revision","Code","Name","Unit","Kind","UnitCents","DurationMinutes","TaxReviewStatus","Archived","CreatedAtUtc","CreatedByOwnerId","UpdatedAtUtc","UpdatedByOwnerId");
        var history=new ReadableTableBuilder(source,budget,"Service revisions","ServiceId","BusinessId","Revision","Name","Unit","Kind","UnitCents","DurationMinutes","TaxReviewStatus","Archived","RecordedByOwnerId","AtUtc");
        ReadableRecords.ForEach<ServiceDefinition>(db,source,"Service",(value,audit)=>
        {
            current.Add(value.Id,source.BusinessId,value.Revision,value.Code,value.Name,value.Unit,value.Kind,value.UnitCents,value.DurationMinutes,value.TaxReviewStatus,value.Archived,audit.CreatedAtUtc,audit.CreatedByOwnerId,audit.UpdatedAtUtc,audit.UpdatedByOwnerId);
            foreach(var version in value.History.OrderBy(h=>h.Revision))history.Add(value.Id,source.BusinessId,version.Revision,version.Name,version.Unit,version.Kind,version.UnitCents,version.DurationMinutes,version.TaxReviewStatus,version.Archived,version.OwnerId,version.AtUtc);
        });return [current.Freeze(),history.Freeze()];
    }
    private static ReadableExportTable[] Bookings(DbWork db,ReadableExportSource source,ReadableExportBudget budget)
    {
        var bookings=new ReadableTableBuilder(source,budget,"Bookings","BookingId","BusinessId","Revision","Name","Archived","PrimaryCustomerId","EventType","EventDate","Venue","ReadyByLocalTime","EventTimeZoneId","ReadyByUtcOffset","Status","PayerMode","SinglePayerCustomerId","UnpaidResponsibility","AcceptedQuoteVersionId","HasTrial","TrialCents","TrialDurationMinutes","CustomDepositCents","TrialDue","DepositDue","FinalDue","NetCents","TaxCents","TotalCents","PlannedTrialCents","PlannedDepositCents","PlannedFinalCents","ActivePlaces","NamedActivePlaces","CreatedAtUtc","CreatedByOwnerId","UpdatedAtUtc","UpdatedByOwnerId");
        var charges=new ReadableTableBuilder(source,budget,"Booking charges","ChargeId","BookingId","BusinessId","Description","Unit","Quantity","UnitCents","DiscountCents","NetCents","TaxRateFraction","PayerCustomerId","Active","ServiceId","ServiceRevision","DurationMinutes");
        var places=new ReadableTableBuilder(source,budget,"Booking places","PlaceId","BookingId","BusinessId","ChargeId","Ordinal","CustomerId","Active");
        ReadableRecords.ForEach<Booking>(db,source,"Booking",(value,audit)=>
        {
            var totals=BookingRules.Totals(value);bookings.Add(value.Id,source.BusinessId,value.Revision,value.Name,value.Archived,value.PrimaryCustomerId,value.EventType,value.EventDate,value.Venue,value.ReadyBy,value.EventTimeZoneId,value.ReadyByUtcOffset,value.Status,value.PayerMode,value.SinglePayerCustomerId,value.UnpaidResponsibility,value.AcceptedQuoteVersionId,value.HasTrial,value.TrialCents,value.TrialDurationMinutes,value.CustomDepositCents,value.TrialDue,value.DepositDue,value.FinalDue,totals.NetCents,totals.TaxCents,totals.TotalCents,totals.Plan?.TrialCents,totals.Plan?.DepositCents,totals.Plan?.FinalCents,value.Places.Count(p=>p.Active),value.Places.Count(p=>p.Active && p.CustomerId.Length>0),audit.CreatedAtUtc,audit.CreatedByOwnerId,audit.UpdatedAtUtc,audit.UpdatedByOwnerId);
            foreach(var charge in value.Charges.OrderBy(c=>c.Id))charges.Add(charge.Id,value.Id,source.BusinessId,charge.Description,charge.Unit,charge.Quantity,charge.UnitCents,charge.DiscountCents,charge.NetCents,charge.TaxRate,charge.PayerCustomerId,charge.Active,charge.ServiceId,charge.ServiceRevision,charge.DurationMinutes);
            foreach(var place in value.Places.OrderBy(p=>p.Id))places.Add(place.Id,value.Id,source.BusinessId,place.ChargeId,place.Ordinal,place.CustomerId,place.Active);
        });return [bookings.Freeze(),charges.Freeze(),places.Freeze()];
    }
}
