using SarahBeauty.Business.Finance;

namespace SarahBeauty.Business.Exports;

internal static class ReadableAssetReportTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget,DateOnly from,DateOnly through)=>snapshot.Read(db=>
    {
        if(from>through)throw new ArgumentException("The report start must be on or before its end.");
        var source=snapshot.Source;var tables=new Tables(source,budget);
        tables.Add(source.GenerationId,AssetSchedule.Read(db,source.BusinessId,from,through),from,"Captured source");
        ReadableBookHistory.ForEachClosed(db,source,(id,period)=>tables.Add(id,period.Reports.Assets,period.Reports.From,"Retained at close"));
        return tables.Freeze();
    });

    private sealed class Tables
    {
        private readonly ReadableExportSource _source;
        private readonly ReadableTableBuilder _positions,_entries,_events,_coverage;
        internal Tables(ReadableExportSource source,ReadableExportBudget budget)
        {
            _source=source;
            _positions=new(source,budget,"Report asset positions","ReportId","BusinessId","AssetId","NameAtCapture","ArchivedAtCapture","LegalOwnerAtCapture","AcquisitionDateAtCapture","InServiceDateAtCapture","RegisteredCostCentsAtCapture","TaxBasisCentsAtCapture","BusinessUseFractionAtCapture","FundingAtCapture","RevisionAtCapture","OpeningCostCents","OpeningDepreciationCents","CostIncreasesCents","CostReductionsCents","DepreciationAddedCents","DepreciationClearedCents","ClosingCostCents","ClosingDepreciationCents","CarryingCents","DisposedAsOf");
            _entries=new(source,budget,"Report asset journal","ReportId","BusinessId","Sequence","AssetId","MovementId","Date","Kind","Description","AccountCode","AccountName","DebitCents","CreditCents","IsBookValue","InSelectedPeriod");
            _events=new(source,budget,"Report asset events","ReportId","BusinessId","Sequence","AssetId","MovementId","Date","Kind","Description","InSelectedPeriod");
            _coverage=new(source,budget,"Asset report detail coverage","ReportId","BusinessId","AssetFacts","SnapshotVersion");
        }
        internal void Add(string id,AssetScheduleSnapshot? snapshot,DateOnly from,string kind)
        {
            if(snapshot is null){_coverage.Add(id,_source.BusinessId,"Not retained",null);return;}
            if(snapshot.Version!=1)throw new InvalidDataException("The retained asset report version is unsupported.");
            _coverage.Add(id,_source.BusinessId,kind,snapshot.Version);
            foreach(var row in snapshot.Positions.OrderBy(p=>p.CurrentFacts.AssetId))AddPosition(id,row);
            int sequence=0;
            foreach(var row in snapshot.Entries)_entries.Add(id,_source.BusinessId,++sequence,row.AssetId,row.MovementId,row.Date,row.Kind,row.Memo,row.AccountCode,row.AccountName,row.DebitCents,row.CreditCents,row.IsBookValue,row.Date>=from);
            sequence=0;
            foreach(var row in snapshot.Events)_events.Add(id,_source.BusinessId,++sequence,row.AssetId,row.MovementId,row.Date,row.Kind,row.Memo,row.Date>=from);
        }
        private void AddPosition(string id,AssetSchedulePosition row)
        {
            var facts=row.CurrentFacts;
            _positions.Add(id,_source.BusinessId,facts.AssetId,facts.Name,facts.Archived,facts.LegalOwner,facts.AcquisitionDate,facts.InServiceDate,facts.CostCents,facts.TaxBasisCents,facts.BusinessUseFraction,facts.Funding,facts.Revision,row.OpeningCostCents,row.OpeningDepreciationCents,row.CostIncreasesCents,row.CostReductionsCents,row.DepreciationAddedCents,row.DepreciationClearedCents,row.ClosingCostCents,row.ClosingDepreciationCents,row.CarryingCents,row.DisposedAsOf);
        }
        internal ReadableExportTable[] Freeze()=>[_positions.Freeze(),_entries.Freeze(),_events.Freeze(),_coverage.Freeze()];
    }
}
