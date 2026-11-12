using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Exports;

internal static class ReadableBookingBillingTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget,DateOnly through)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;var tables=new Tables(source,budget);var versions=DocumentVersions.List(db,source.BusinessId);
        tables.Coverage(source.GenerationId,"Captured source",1);
        foreach(var booking in RecordStore<Booking>.ReadBusiness(db,source.BusinessId,"Booking").OrderBy(b=>b.Id))tables.Add(source.GenerationId,BookingBillingSources.Read(db,source.BusinessId,booking,through,versions,source.CapturedAtUtc));
        ReadableBookHistory.ForEachClosed(db,source,(id,period)=>
        {
            if(period.Reports.Bookings is not {} bookings){tables.Coverage(id,"Not retained",null);return;}
            if(bookings.Version!=1)throw new InvalidDataException("The retained booking report version is unsupported.");
            tables.Coverage(id,"Retained at close",bookings.Version);
            foreach(var row in bookings.Positions.OrderBy(p=>p.CurrentFacts.BookingId))tables.Add(id,row.Billing);
        });
        return tables.Freeze();
    });

    private sealed class Tables
    {
        private readonly ReadableExportSource _source;
        private readonly ReadableTableBuilder _scopes,_invoices,_credits,_lines,_dues,_coverage;
        internal Tables(ReadableExportSource source,ReadableExportBudget budget)
        {
            _source=source;
            _scopes=new(source,budget,"Booking billing report scopes","ReportId","BusinessId","BookingId","NameAtCapture","RevisionAtCapture","ObservedMode","ObservedWatermark","GeneratedAtUtc","AsOf");
            _invoices=new(source,budget,"Report booking invoice balances","ReportId","BusinessId","BookingId","InvoiceVersionId","Number","SourceQuoteVersionId","PayerId","FrozenPayerName","IssueDate","DueDate","OriginalNetCents","OriginalTaxCents","ReducedNetCents","ReducedTaxCents","EffectiveNetCents","EffectiveTaxCents","AppliedCents","AppliedTaxCents","OpenCents","OpenTaxCents","SourceSha256");
            _credits=new(source,budget,"Report booking invoice credits","ReportId","BusinessId","BookingId","CreditNoteVersionId","InvoiceVersionId","Number","Date","NetCents","TaxCents","SourceSha256");
            _lines=new(source,budget,"Report booking billed lines","ReportId","BusinessId","BookingId","Kind","VersionId","Sequence","ChargeId","Description","Unit","Quantity","UnitCents","DiscountCents","NetCents","TaxCents","TotalCents","RoundingCents");
            _dues=new(source,budget,"Report booking milestone dues","ReportId","BusinessId","BookingId","InvoiceVersionId","InvoiceNumber","PayerId","FrozenPayerName","MilestoneId","Kind","DueDate","Cents","TaxCents");
            _coverage=new(source,budget,"Booking billing report coverage","ReportId","BusinessId","BillingFacts","SnapshotVersion");
        }
        internal void Coverage(string id,string kind,int? version)=>_coverage.Add(id,_source.BusinessId,kind,version);
        internal void Add(string id,BookingBillingSnapshot billing)
        {
            _scopes.Add(id,_source.BusinessId,billing.BookingId,billing.CurrentBookingName,billing.CurrentBookingRevision,billing.Mode,billing.Watermark,billing.GeneratedAtUtc,billing.AsOf);
            foreach(var row in billing.Invoices.OrderBy(i=>i.VersionId))
            {
                _invoices.Add(id,_source.BusinessId,billing.BookingId,row.VersionId,row.Number,row.SourceQuoteVersionId,row.PayerId,row.PayerName,row.IssueDate,row.DueDate,row.OriginalNetCents,row.OriginalTaxCents,row.ReducedNetCents,row.ReducedTaxCents,row.EffectiveNetCents,row.EffectiveTaxCents,row.AppliedCents,row.AppliedTaxCents,row.OpenCents,row.OpenTaxCents,row.SourceHash);
                Lines(id,billing.BookingId,"Invoice",row.VersionId,row.Lines);
            }
            foreach(var row in billing.Credits.OrderBy(c=>c.VersionId)){_credits.Add(id,_source.BusinessId,billing.BookingId,row.VersionId,row.InvoiceVersionId,row.Number,row.Date,row.NetCents,row.TaxCents,row.SourceHash);Lines(id,billing.BookingId,"CreditNote",row.VersionId,row.Lines);}
            foreach(var row in billing.Dues.OrderBy(d=>d.MilestoneId))_dues.Add(id,_source.BusinessId,billing.BookingId,row.InvoiceVersionId,row.Number,row.CustomerId,row.CustomerName,row.MilestoneId,row.Kind,row.DueDate,row.Cents,row.TaxCents);
        }
        private void Lines(string id,string booking,string kind,string version,IReadOnlyList<ClientLine> lines)
        {
            for(int index=0;index<lines.Count;index++){var row=lines[index];_lines.Add(id,_source.BusinessId,booking,kind,version,index+1,row.ChargeId,row.Description,row.Unit,row.Quantity,row.UnitCents,row.DiscountCents,row.NetCents,row.TaxCents,row.TotalCents,row.RoundingCents);}
        }
        internal ReadableExportTable[] Freeze()=>[_scopes.Freeze(),_invoices.Freeze(),_credits.Freeze(),_lines.Freeze(),_dues.Freeze(),_coverage.Freeze()];
    }
}
