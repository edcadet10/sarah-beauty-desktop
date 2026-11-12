using SarahBeauty.Business.Finance;

namespace SarahBeauty.Business.Exports;

internal static class ReadableDebtReportTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget,DateOnly from,DateOnly through)=>snapshot.Read(db=>
    {
        if(from>through)throw new ArgumentException("The report start must be on or before its end.");
        var source=snapshot.Source;var tables=new Tables(source,budget);
        tables.Add(source.GenerationId,DebtSchedule.Read(db,source.BusinessId,from,through),from,"Captured source");
        ReadableBookHistory.ForEachClosed(db,source,(id,period)=>tables.Add(id,period.Reports.Debts,period.Reports.From,"Retained at close"));
        return tables.Freeze();
    });

    private sealed class Tables
    {
        private readonly ReadableExportSource _source;
        private readonly ReadableTableBuilder _positions,_entries,_coverage;
        internal Tables(ReadableExportSource source,ReadableExportBudget budget)
        {
            _source=source;
            _positions=new(source,budget,"Report debt positions","ReportId","BusinessId","LoanId","NameAtCapture","ArchivedAtCapture","RegisteredOpeningDateAtCapture","RegisteredOpeningPrincipalCentsAtCapture","DueDateAtCapture","TermsAtCapture","OwnerLenderIdAtCapture","OwnerLenderNameAtCapture","RevisionAtCapture","PostedOpeningPrincipalCents","OpeningAdjustmentsCents","ProceedsCents","PrincipalPaidCents","OtherPrincipalChangeCents","InterestCents","FeesCents","ClosingPrincipalCents");
            _entries=new(source,budget,"Report debt journal","ReportId","BusinessId","Sequence","LoanId","MovementId","Date","Kind","EffectiveKind","Description","AccountCode","AccountName","DebitCents","CreditCents","LenderFactsCaptured","CapturedLoanId","CapturedLenderName","CapturedOwnerLenderId","CapturedOwnerLenderName","InSelectedPeriod");
            _coverage=new(source,budget,"Debt report detail coverage","ReportId","BusinessId","DebtFacts","SnapshotVersion");
        }
        internal void Add(string id,DebtScheduleSnapshot? snapshot,DateOnly from,string kind)
        {
            if(snapshot is null){_coverage.Add(id,_source.BusinessId,"Not retained",null);return;}
            if(snapshot.Version!=1)throw new InvalidDataException("The retained debt report version is unsupported.");
            _coverage.Add(id,_source.BusinessId,kind,snapshot.Version);
            foreach(var row in snapshot.Positions.OrderBy(p=>p.CurrentFacts.LoanId))
            {
                var facts=row.CurrentFacts;
                _positions.Add(id,_source.BusinessId,facts.LoanId,facts.Name,facts.Archived,facts.OpeningDate,facts.OpeningPrincipalCents,facts.DueDate,facts.Terms,facts.OwnerLenderId,facts.OwnerLenderName,facts.Revision,row.PostedOpeningPrincipalCents,row.OpeningAdjustmentsCents,row.ProceedsCents,row.PrincipalPaidCents,row.OtherPrincipalChangeCents,row.InterestCents,row.FeesCents,row.ClosingPrincipalCents);
            }
            int sequence=0;
            foreach(var row in snapshot.Entries)
            {
                var lender=row.CapturedLender;
                _entries.Add(id,_source.BusinessId,++sequence,row.LoanId,row.MovementId,row.Date,row.Kind,row.EffectiveKind,row.Memo,row.AccountCode,row.AccountName,row.DebitCents,row.CreditCents,lender is not null,lender?.LoanId,lender?.LenderName,lender?.OwnerLenderId,lender?.OwnerLenderName,row.Date>=from);
            }
        }
        internal ReadableExportTable[] Freeze()=>[_positions.Freeze(),_entries.Freeze(),_coverage.Freeze()];
    }
}
