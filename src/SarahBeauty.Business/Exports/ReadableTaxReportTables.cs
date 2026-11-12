using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Business.Exports;

internal static class ReadableTaxReportTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget,DateOnly from,DateOnly through)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;var tables=new Tables(source,budget);
        var trial=LedgerReportKernel.TrialBalance(db,source.BusinessId,from,through);
        tables.Summary(source.GenerationId,TaxReportKernel.Read(db,source.BusinessId,from,through,trial,DocumentVersions.List(db,source.BusinessId)));
        tables.Filings(source.GenerationId,TaxFilingReports.Read(db,source.BusinessId,from,through),"Captured source");
        ReadableBookHistory.ForEachClosed(db,source,(id,period)=>
        {
            tables.Summary(id,period.Reports.Tax);tables.Filings(id,period.Reports.TaxFilings,"Retained at close");
        });
        return tables.Freeze();
    });

    private sealed class Tables
    {
        private readonly ReadableExportSource _source;
        private readonly ReadableTableBuilder _tax,_periods,_history,_coverage;
        internal Tables(ReadableExportSource source,ReadableExportBudget budget)
        {
            _source=source;
            _tax=new(source,budget,"Report tax amounts","ReportId","BusinessId","ChargedCents","ChargeReductionCents","CollectedCents","RefundedCents","RemittedCents","LiabilityCents","RecoverableCents","RecoveredCashCents","OffsetCents","NonrecoverableCents");
            _periods=new(source,budget,"Report tax filing periods","ReportId","BusinessId","PeriodId","NameAtCapture","Jurisdiction","From","Through","DueDate","StatusAtCapture","ArchivedAtCapture","RevisionAtCapture");
            _history=new(source,budget,"Report tax filing decisions","ReportId","BusinessId","PeriodId","Sequence","RequestId","ReviewedRevision","Status","FilingDate","ReportedTaxCents","Reference","OwnerId","OwnerNameAtCapture","AtUtc");
            _coverage=new(source,budget,"Tax report filing coverage","ReportId","BusinessId","FilingFacts","SnapshotVersion");
        }
        internal void Summary(string id,TaxPosition row)=>_tax.Add(id,_source.BusinessId,row.ChargedCents,row.ChargeReductionCents,row.CollectedCents,row.RefundedCents,row.RemittedCents,row.LiabilityCents,row.RecoverableCents,row.RecoveredCashCents,row.OffsetCents,row.NonrecoverableCents);
        internal void Filings(string id,TaxFilingSnapshot? snapshot,string kind)
        {
            if(snapshot is null){_coverage.Add(id,_source.BusinessId,"Not retained",null);return;}
            if(snapshot.Version!=1)throw new InvalidDataException("The retained tax filing report version is unsupported.");
            _coverage.Add(id,_source.BusinessId,kind,snapshot.Version);
            foreach(var row in snapshot.Periods.OrderBy(p=>p.Id))
            {
                _periods.Add(id,_source.BusinessId,row.Id,row.Name,row.Jurisdiction,row.From,row.Through,row.DueDate,row.StatusAtCapture,row.Archived,row.Revision);
                for(int index=0;index<row.History.Length;index++)
                {
                    var item=row.History[index];var decision=item.Decision;
                    _history.Add(id,_source.BusinessId,row.Id,index+1,decision.RequestId,decision.ExpectedRevision,decision.Status,decision.FilingDate,decision.ReportedTaxCents,decision.Reference,item.OwnerId,item.OwnerNameAtCapture,item.AtUtc);
                }
            }
        }
        internal ReadableExportTable[] Freeze()=>[_tax.Freeze(),_periods.Freeze(),_history.Freeze(),_coverage.Freeze()];
    }
}
