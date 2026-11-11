using System.Numerics;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class BookPeriodRules
{
    internal static BookPeriodPreview Preview(DbWork db,BusinessStore store,OwnerSession owner,BookPeriodDraft draft)
    {
        if(draft.From>draft.Through || string.IsNullOrWhiteSpace(draft.Reason) || string.IsNullOrWhiteSpace(draft.EvidenceId))
            throw new ArgumentException("Choose a valid book period and record the closing review reason and evidence.");
        var report=new FinancialReports(store).Load(db,owner,draft.From,draft.Through);
        var closed=ClosedThrough(db,owner);var issues=report.OpeningWarnings.ToList();
        if(closed.HasValue && (closed.Value==DateOnly.MaxValue || draft.From!=closed.Value.AddDays(1)))issues.Add("Continue from the day after the current closed-through date, or reopen the affected periods first.");
        if(!closed.HasValue && EarliestDate(db,owner) is DateOnly earliest && draft.From>earliest)issues.Add($"The first closing report must include the earliest recorded financial date, {earliest:yyyy-MM-dd}.");
        if(!report.BalanceSheet.Balances || Sum(report.TrialBalance.Select(r=>r.Opening))!=0 || Sum(report.TrialBalance.Select(r=>r.Debits))!=Sum(report.TrialBalance.Select(r=>r.Credits)))
            issues.Add("Resolve the trial-balance or balance-sheet difference before closing.");
        var reconciliations=Reconciliations(db,owner,draft.Through);
        foreach(var cash in report.TrialBalance.Where(r=>r.Account.CashKind!="None" && (r.Account.Active || r.Ending!=0 || r.Debits!=0 || r.Credits!=0)))
        {
            var saved=reconciliations.SingleOrDefault(r=>r.Snapshot.Sources.Statement.AccountId==cash.Account.Id);
            if(!cash.Account.OpeningConfirmed)issues.Add(cash.Account.Name+": confirm the cash opening.");
            if(saved is null)issues.Add(cash.Account.Name+": complete a reconciliation at the period end.");
            else if(!saved.Snapshot.ReadyToComplete || saved.Snapshot.AdjustedLedgerCents!=cash.Ending)issues.Add(cash.Account.Name+": the retained reconciliation does not agree with the current closing balance.");
        }
        return new(draft,closed,report,issues.Distinct().ToArray(),reconciliations.Select(r=>new ReconciliationSummary(r.Id,r.AccountId,r.StatementId,r.AsOfDate,r.ApprovedBy,r.AtUtc,false)).ToArray());
    }

    internal static DateOnly? ClosedThrough(DbWork db,OwnerSession owner)=>db.Scalar("SELECT ClosedThrough FROM BookSettings WHERE BusinessId=$business;",("$business",owner.BusinessId)) is string date?DateOnly.Parse(date):null;
    private static BigInteger Sum(IEnumerable<long> values)=>values.Aggregate(BigInteger.Zero,(sum,value)=>sum+value);

    private static CompletedReconciliation[] Reconciliations(DbWork db,OwnerSession owner,DateOnly through)
    {
        var ids=db.Read("""
            SELECT C.Id FROM Reconciliations C WHERE C.BusinessId=$business AND C.AsOfDate=$date
            AND NOT EXISTS(SELECT 1 FROM ReconciliationReopens R WHERE R.ReconciliationId=C.Id);
            """,r=>r.GetString(0),("$business",owner.BusinessId),("$date",through.ToString("yyyy-MM-dd")));
        return ids.Select(id=>ReconciliationQueries.List(db,owner,id).Single()).ToArray();
    }

    private static DateOnly? EarliestDate(DbWork db,OwnerSession owner)
    {
        var date=db.Scalar("""
            SELECT MIN(Date) FROM (
                SELECT Date FROM Movements WHERE BusinessId=$business
                UNION ALL SELECT Date FROM OpeningBalances WHERE BusinessId=$business
                UNION ALL SELECT V.IssueDate FROM DocumentVersions V JOIN Documents D ON D.Id=V.DocumentId WHERE D.BusinessId=$business AND D.Type IN ('Invoice','CreditNote')
                UNION ALL SELECT json_extract(DataJson,'$.PurchaseDate') FROM Records WHERE BusinessId=$business AND Kind='Purchase' AND json_extract(DataJson,'$.Status')='Approved'
            );
            """,("$business",owner.BusinessId)) as string;
        return date is null?null:DateOnly.Parse(date);
    }
}
