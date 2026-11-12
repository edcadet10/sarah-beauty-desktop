using SarahBeauty.Business.Dashboard;

namespace SarahBeauty.Business.Exports;

internal static class ReadableDashboardUpcomingTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget,DateOnly asOf,DateOnly from,DateOnly through)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;var value=DashboardUpcomingSources.Read(db,source.BusinessId,asOf,from,through,source.CapturedAtUtc);var tables=new Tables(source,budget);
        tables.Add(value);return tables.Freeze();
    });

    private sealed class Tables
    {
        private readonly ReadableExportSource _source;
        private readonly ReadableTableBuilder _scopes,_months,_members,_bookings,_issues,_artists,_invoices,_dues;
        internal Tables(ReadableExportSource source,ReadableExportBudget budget)
        {
            _source=source;
            _scopes=new(source,budget,"Dashboard upcoming planning scope","ReportId","BusinessId","BusinessName","Mode","ObservedWatermark","GeneratedAtUtc","PlanningAsOf","EventFrom","EventThrough","ValueBasis");
            _months=new(source,budget,"Dashboard upcoming monthly data","ReportId","BusinessId","Month","ConfirmedCount","KnownAgreementGrossCents","UnknownValueCount");
            _members=new(source,budget,"Dashboard upcoming month bookings","ReportId","BusinessId","Month","BookingId");
            _bookings=new(source,budget,"Dashboard upcoming booking values","ReportId","BusinessId","DateGroup","BookingId","NameAtCapture","EventDate","Venue","ReadyByLocalTime","TimeZoneId","QuoteVersionId","QuoteLabel","QuoteSha256","AgreementGrossCents");
            _issues=new(source,budget,"Dashboard upcoming review issues","ReportId","BusinessId","BookingId","Sequence","Issue");
            _artists=new(source,budget,"Dashboard upcoming artists","ReportId","BusinessId","BookingId","ArtistId","NameAtCapture","StateAtCapture");
            _invoices=new(source,budget,"Dashboard upcoming invoice balances","ReportId","BusinessId","BookingId","PlanningAsOf","BillingWatermark","InvoiceVersionId","SourceQuoteVersionId","FrozenPayerName","OriginalNetCents","OriginalTaxCents","ReducedNetCents","ReducedTaxCents","AppliedCents","OpenCents","SourceSha256");
            _dues=new(source,budget,"Dashboard upcoming milestone dues","ReportId","BusinessId","BookingId","InvoiceVersionId","InvoiceNumber","PayerId","FrozenPayerName","MilestoneId","Kind","DueDate","Cents","TaxCents");
        }
        internal void Add(DashboardUpcomingSnapshot value)
        {
            _scopes.Add(_source.GenerationId,_source.BusinessId,value.BusinessName,value.Mode,value.Watermark,value.GeneratedAtUtc,value.PlanningAsOf,value.From,value.Through,"Confirmed agreement gross including tax; known subtotal excludes unknown values; not cash income");
            foreach(var row in value.Months)
            {
                _months.Add(_source.GenerationId,_source.BusinessId,row.Month,row.Count,row.KnownAgreementGrossCents,row.UnknownValueCount);
                foreach(string id in row.BookingIds.Order())_members.Add(_source.GenerationId,_source.BusinessId,row.Month,id);
            }
            foreach(var row in value.Bookings)AddBooking("Dated",row);
            foreach(var row in value.Undated.OrderBy(b=>b.BookingId))AddBooking("Undated",row);
        }
        private void AddBooking(string group,DashboardUpcomingBooking row)
        {
            string report=_source.GenerationId;string business=_source.BusinessId;string booking=row.BookingId;
            _bookings.Add(report,business,group,booking,row.Name,row.EventDate,row.Venue,row.ReadyBy,row.TimeZoneId,row.QuoteVersionId,row.QuoteLabel,row.QuoteHash,row.AgreementGrossCents);
            for(int index=0;index<row.ReviewIssues.Length;index++)_issues.Add(report,business,booking,index+1,row.ReviewIssues[index]);
            foreach(var artist in row.Artists.OrderBy(a=>a.Id))_artists.Add(report,business,booking,artist.Id,artist.Name,artist.State);
            foreach(var invoice in row.Billing.Invoices.OrderBy(i=>i.VersionId))_invoices.Add(report,business,booking,row.Billing.AsOf,row.Billing.Watermark,invoice.VersionId,invoice.SourceQuoteVersionId,invoice.PayerName,invoice.OriginalNetCents,invoice.OriginalTaxCents,invoice.ReducedNetCents,invoice.ReducedTaxCents,invoice.AppliedCents,invoice.OpenCents,invoice.SourceHash);
            foreach(var due in row.Billing.Dues.OrderBy(d=>d.MilestoneId))_dues.Add(report,business,booking,due.InvoiceVersionId,due.Number,due.CustomerId,due.CustomerName,due.MilestoneId,due.Kind,due.DueDate,due.Cents,due.TaxCents);
        }
        internal ReadableExportTable[] Freeze()=>[_scopes.Freeze(),_months.Freeze(),_members.Freeze(),_bookings.Freeze(),_issues.Freeze(),_artists.Freeze(),_invoices.Freeze(),_dues.Freeze()];
    }
}
