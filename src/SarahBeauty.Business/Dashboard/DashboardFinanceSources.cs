using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Dashboard;

public sealed class DashboardFinanceSources(BusinessStore store)
{
    internal Action? AfterReportsRead {get;set;}
    public DashboardFinanceSnapshot Load(OwnerSession owner,DateOnly from,DateOnly through)=>store.ReadSnapshot(db=>Load(db,owner,from,through));

    internal DashboardFinanceSnapshot Load(DbWork db,OwnerSession owner,DateOnly from,DateOnly through)
    {
        store.RequireSession(db,owner);
        int months=(through.Year-from.Year)*12+through.Month-from.Month+1;
        if(from>through || months>120)throw new ArgumentException("Choose a dashboard financial window of at most 120 calendar months, ending on or after its start.");
        var reports=new FinancialReports(store).Load(db,owner,from,through);AfterReportsRead?.Invoke();
        var expenses=reports.TrialBalance.Where(t=>t.Account.Type=="Expense").Select(t=>new DashboardExpense(t.Account.Id,t.Account.Code,t.Account.Name,checked(t.Debits-t.Credits))).ToArray();
        return new(reports,DashboardMonthly.Read(db,owner,reports,months),DashboardAging.Read(reports),expenses,CashAccounts(db,owner,reports),CashEntries(db,owner,through));
    }

    private static DashboardCashAccount[] CashAccounts(DbWork db,OwnerSession owner,ReportSnapshot reports)
    {
        var ids=db.Read("""
            SELECT C.AccountId,C.Id FROM Reconciliations C WHERE C.BusinessId=$business AND C.AsOfDate<=$date
            AND NOT EXISTS(SELECT 1 FROM ReconciliationReopens R WHERE R.ReconciliationId=C.Id) ORDER BY C.AsOfDate DESC,C.AtUtc DESC,C.Id DESC;
            """,r=>(Account:r.GetString(0),Id:r.GetString(1)),("$business",owner.BusinessId),("$date",reports.Through.ToString("yyyy-MM-dd")))
            .GroupBy(c=>c.Account).ToDictionary(g=>g.Key,g=>g.First().Id);
        return reports.TrialBalance.Where(t=>t.Account.CashKind!="None").Select(t=>
        {
            var reconciliation=ids.TryGetValue(t.Account.Id,out string? id)?ReconciliationQueries.List(db,owner,id).Single():null;
            return new DashboardCashAccount(t.Account,t.Ending,reconciliation?.Id??"",reconciliation?.AsOfDate,reconciliation?.SnapshotHash??"");
        }).ToArray();
    }

    private static CashActivity[] CashEntries(DbWork db,OwnerSession owner,DateOnly through)=>db.Read("""
        SELECT M.Id,M.Date,M.Kind,M.Memo,A.Id,A.Name,J.DebitCents,J.CreditCents FROM Movements M
        JOIN JournalLines J ON J.MovementId=M.Id JOIN Accounts A ON A.Id=J.AccountId
        WHERE M.BusinessId=$business AND M.Date<=$date AND A.CashKind<>'None' ORDER BY M.Date,M.rowid,J.rowid;
        """,r=>new CashActivity(r.GetString(0),DateOnly.Parse(r.GetString(1)),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetInt64(6),r.GetInt64(7)),
        ("$business",owner.BusinessId),("$date",through.ToString("yyyy-MM-dd"))).ToArray();
}
