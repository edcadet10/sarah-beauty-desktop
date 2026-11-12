using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Business.Exports;

internal static class ReadableBookingFinancialTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget,DateOnly from,DateOnly through)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;var tables=new Tables(source,budget);
        tables.Add(source.GenerationId,BookingContributionReports.Read(db,source.BusinessId,from,through,DocumentVersions.List(db,source.BusinessId),source.CapturedAtUtc),"Captured source");
        ReadableBookHistory.ForEachClosed(db,source,(id,period)=>tables.Add(id,period.Reports.Bookings,"Retained at close"));
        return tables.Freeze();
    });

    private sealed class Tables
    {
        private readonly ReadableExportSource _source;
        private readonly ReadableTableBuilder _summary,_entries,_cash,_unassigned,_coverage;
        private static readonly string[] EntryColumns=["ReportId","BusinessId","BookingId","Sequence","MovementId","Date","Kind","EffectiveKind","Description","AccountCode","AccountName","DebitCents","CreditCents","IsDirectCost","IsServiceIncome"];
        internal Tables(ReadableExportSource source,ReadableExportBudget budget)
        {
            _source=source;
            _summary=new(source,budget,"Report booking contribution","ReportId","BusinessId","BookingId","NameAtCapture","StatusAtCapture","ArchivedAtCapture","BillingWatermark","BillingGeneratedAtUtc","BillingAsOf","FinancialWatermark","FinancialGeneratedAtUtc","FinancialFrom","FinancialThrough","EffectiveNetChargesCents","PeriodNetChargesCents","ContributionEstimateCents","AppliedCents","OpenCents","RecordedDirectCostCents","PeriodRecordedDirectCostCents","RecognizedServiceIncomeCents","PeriodRecognizedServiceIncomeCents");
            _entries=new(source,budget,"Report booking financial journal",EntryColumns);
            _unassigned=new(source,budget,"Report unassigned direct costs",EntryColumns);
            _cash=new(source,budget,"Report booking tagged cash","ReportId","BusinessId","BookingId","Sequence","MovementId","Date","Kind","EffectiveKind","Description","CashAccountId","SignedGrossCents","SignedTaxCents","SignedFeeCents","SignedNetCashCents");
            _coverage=new(source,budget,"Booking financial report coverage","ReportId","BusinessId","BookingFacts","SnapshotVersion");
        }
        internal void Add(string id,BookingReportSnapshot? snapshot,string kind)
        {
            if(snapshot is null){_coverage.Add(id,_source.BusinessId,"Not retained",null);return;}
            if(snapshot.Version!=1)throw new InvalidDataException("The retained booking report version is unsupported.");
            _coverage.Add(id,_source.BusinessId,kind,snapshot.Version);
            foreach(var row in snapshot.Positions.OrderBy(p=>p.CurrentFacts.BookingId))AddPosition(id,row);
            AddEntries(_unassigned,id,null,snapshot.UnassignedDirectCosts);
        }
        private void AddPosition(string id,BookingReportPosition row)
        {
            var facts=row.CurrentFacts;var billing=row.Billing;var finance=row.Financial;
            if(facts.BookingId!=billing.BookingId||facts.BookingId!=finance.BookingId||billing.Watermark!=finance.Watermark||billing.AsOf!=finance.Through)throw new InvalidDataException("The booking report sources do not match their booking, watermark, or date scope.");
            _summary.Add(id,_source.BusinessId,facts.BookingId,facts.Name,facts.Status,facts.Archived,billing.Watermark,billing.GeneratedAtUtc,billing.AsOf,finance.Watermark,finance.GeneratedAtUtc,finance.From,finance.Through,row.EffectiveNetChargesCents,row.PeriodNetChargesCents,row.ContributionEstimateCents,row.AppliedCents,row.OpenCents,finance.RecordedDirectCostCents,finance.PeriodRecordedDirectCostCents,finance.RecognizedServiceIncomeCents,finance.PeriodRecognizedServiceIncomeCents);
            AddEntries(_entries,id,facts.BookingId,finance.Entries);int sequence=0;
            foreach(var cash in finance.Cash)_cash.Add(id,_source.BusinessId,facts.BookingId,++sequence,cash.MovementId,cash.Date,cash.Kind,cash.EffectiveKind,cash.Memo,cash.CashAccountId,cash.SignedGrossCents,cash.SignedTaxCents,cash.SignedFeeCents,cash.SignedNetCashCents);
        }
        private void AddEntries(ReadableTableBuilder table,string report,string? booking,IEnumerable<BookingFinancialEntry> entries)
        {
            int sequence=0;
            foreach(var row in entries)table.Add(report,_source.BusinessId,booking,++sequence,row.MovementId,row.Date,row.Kind,row.EffectiveKind,row.Memo,row.AccountCode,row.AccountName,row.DebitCents,row.CreditCents,row.IsDirectCost,row.IsServiceIncome);
        }
        internal ReadableExportTable[] Freeze()=>[_summary.Freeze(),_entries.Freeze(),_cash.Freeze(),_unassigned.Freeze(),_coverage.Freeze()];
    }
}
