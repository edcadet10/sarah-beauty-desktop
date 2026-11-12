using SarahBeauty.Business.Finance;

namespace SarahBeauty.Business.Exports;

internal static class ReadableOwnerReportTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget,DateOnly from,DateOnly through)=>snapshot.Read(db=>
    {
        if(from>through)throw new ArgumentException("The report start must be on or before its end.");
        var source=snapshot.Source;var tables=new Tables(source,budget);
        tables.Add(source.GenerationId,OwnerSchedule.Read(db,source.BusinessId,from,through,DebtSchedule.Read(db,source.BusinessId,from,through)),from,"Captured source");
        ReadableBookHistory.ForEachClosed(db,source,(id,period)=>tables.Add(id,period.Reports.Owners,period.Reports.From,"Retained at close"));
        return tables.Freeze();
    });

    private sealed class Tables
    {
        private readonly ReadableExportSource _source;
        private readonly ReadableTableBuilder _positions,_accounts,_entries,_loans,_coverage;
        internal Tables(ReadableExportSource source,ReadableExportBudget budget)
        {
            _source=source;
            _positions=new(source,budget,"Report owner positions","ReportId","BusinessId","OwnerId","NameAtCapture","ActiveAtCapture","ContributionsCents","DrawsCents","PayableAddedCents","PayableClearedCents","ReimbursementsCents","LoanProceedsCents","LoanPrincipalPaidCents","AttributedLoanPrincipalCents");
            _accounts=new(source,budget,"Report owner account balances","ReportId","BusinessId","OwnerId","AccountCode","OpeningDebitLessCreditCents","PeriodDebitCents","PeriodCreditCents","ClosingDebitLessCreditCents");
            _entries=new(source,budget,"Report owner attributed journal","ReportId","BusinessId","Sequence","OwnerId","MovementId","Date","Kind","EffectiveKind","Description","AccountCode","AccountName","DebitCents","CreditCents","Attribution","LoanId","InSelectedPeriod");
            _loans=new(source,budget,"Report owner loan attribution","ReportId","BusinessId","LoanId","LoanNameAtCapture","AttributedOwnerId","Attribution","PrincipalCents");
            _coverage=new(source,budget,"Owner report detail coverage","ReportId","BusinessId","OwnerFacts","SnapshotVersion");
        }
        internal void Add(string id,OwnerScheduleSnapshot? snapshot,DateOnly from,string kind)
        {
            if(snapshot is null){_coverage.Add(id,_source.BusinessId,"Not retained",null);return;}
            if(snapshot.Version!=1)throw new InvalidDataException("The retained owner report version is unsupported.");
            _coverage.Add(id,_source.BusinessId,kind,snapshot.Version);
            foreach(var row in snapshot.Positions.OrderBy(p=>p.OwnerId))_positions.Add(id,_source.BusinessId,row.OwnerId,row.Name,row.Active,row.ContributionsCents,row.DrawsCents,row.PayableAddedCents,row.PayableClearedCents,row.ReimbursementsCents,row.LoanProceedsCents,row.LoanPrincipalPaidCents,row.AttributedLoanPrincipalCents);
            foreach(var row in snapshot.Accounts.OrderBy(p=>p.OwnerId).ThenBy(p=>p.AccountCode))_accounts.Add(id,_source.BusinessId,row.OwnerId,row.AccountCode,row.OpeningDebitLessCreditCents,row.PeriodDebitCents,row.PeriodCreditCents,row.ClosingDebitLessCreditCents);
            int sequence=0;
            foreach(var row in snapshot.Entries)_entries.Add(id,_source.BusinessId,++sequence,row.OwnerId,row.MovementId,row.Date,row.Kind,row.EffectiveKind,row.Memo,row.AccountCode,row.AccountName,row.DebitCents,row.CreditCents,row.Attribution,row.LoanId,row.Date>=from);
            foreach(var row in snapshot.LoanBalances.OrderBy(p=>p.LoanId))_loans.Add(id,_source.BusinessId,row.LoanId,row.LoanName,row.OwnerId,row.Attribution,row.PrincipalCents);
        }
        internal ReadableExportTable[] Freeze()=>[_positions.Freeze(),_accounts.Freeze(),_entries.Freeze(),_loans.Freeze(),_coverage.Freeze()];
    }
}
