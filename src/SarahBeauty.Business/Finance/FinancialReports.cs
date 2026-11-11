using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

public sealed class FinancialReports(BusinessStore store)
{
    internal Action? AfterTrialRead {get;set;}
    public ReportSnapshot Load(OwnerSession owner,DateOnly from,DateOnly through)=>store.ReadSnapshot(db=>
    {
        store.RequireSession(db,owner);
        var confirmed=ConfirmedOpenings(db,owner,through);
        var trial=LedgerReports.TrialBalance(db,owner,from,through)
            .Select(r=>r with{Account=r.Account with{OpeningConfirmed=confirmed.Contains(r.Account.Id)}}).ToList();
        AfterTrialRead?.Invoke();
        var pnl=new ProfitAndLoss(trial.Where(r=>r.Account.Type=="Income").Sum(r=>r.Credits-r.Debits),trial.Where(r=>r.Account.Type=="Expense").Sum(r=>r.Debits-r.Credits));
        var balance=new BalanceSheet(trial.Where(r=>r.Account.Type=="Asset").Sum(r=>r.Ending),-trial.Where(r=>r.Account.Type=="Liability").Sum(r=>r.Ending),
            -trial.Where(r=>r.Account.Type is "Equity" or "Income" or "Expense").Sum(r=>r.Ending));
        string business=(string)db.Scalar("SELECT DisplayName FROM Business WHERE Id=$id;",("$id",owner.BusinessId))!;
        long watermark=ReceiptSnapshot.Watermark(db,owner);
        var versions=DocumentVersions.List(db,owner);
        var entries=Activity(db,owner,from,through);
        var cashAccounts=trial.Where(r=>r.Account.CashKind!="None").Select(r=>r.Account.Id).ToHashSet();
        return new ReportSnapshot(business,"TEST",watermark,DateTimeOffset.UtcNow.ToString("O"),from,through,trial,pnl,balance,entries.Where(e=>cashAccounts.Contains(e.AccountId)).ToList(),
            CustomerPositions.Read(db,owner,through,versions),Tax(db,owner,from,through,trial,versions),OpeningWarnings(db,owner,trial)){Entries=entries};
    });

    private static List<CashActivity> Activity(DbWork db,OwnerSession owner,DateOnly from,DateOnly through)=>db.Read("""
        SELECT M.Id,M.Date,M.Kind,M.Memo,A.Id,A.Name,J.DebitCents,J.CreditCents FROM JournalLines J JOIN Movements M ON M.Id=J.MovementId
        JOIN Accounts A ON A.Id=J.AccountId WHERE M.BusinessId=$business
        AND M.Date BETWEEN $from AND $through ORDER BY M.Date,M.Id,A.Code;
        """,r=>new CashActivity(r.GetString(0),DateOnly.Parse(r.GetString(1)),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetInt64(6),r.GetInt64(7)),
        ("$business",owner.BusinessId),("$from",from.ToString("yyyy-MM-dd")),("$through",through.ToString("yyyy-MM-dd")));

    private static TaxPosition Tax(DbWork db,OwnerSession owner,DateOnly from,DateOnly through,List<AccountTotal> trial,List<DocumentVersion> versions)
    {
        var documents=versions.Where(v=>v.Snapshot.IssueDate>=from && v.Snapshot.IssueDate<=through).ToList();
        var tax=db.Read("""
            SELECT CASE WHEN M.Kind='Reversal' THEN O.Kind ELSE M.Kind END,SUM(J.CreditCents-J.DebitCents)
            FROM Movements M JOIN JournalLines J ON J.MovementId=M.Id JOIN Accounts A ON A.Id=J.AccountId LEFT JOIN Movements O ON O.Id=M.ReversesId
            WHERE M.BusinessId=$business AND A.Code='2000' AND M.Date BETWEEN $from AND $through GROUP BY CASE WHEN M.Kind='Reversal' THEN O.Kind ELSE M.Kind END;
            """,r=>(Kind:r.GetString(0),Cents:r.GetInt64(1)),("$business",owner.BusinessId),("$from",from.ToString("yyyy-MM-dd")),("$through",through.ToString("yyyy-MM-dd")));
        return new(documents.Where(v=>v.Snapshot.Kind==DocumentKind.Invoice && v.Status!="Void").Sum(v=>v.Snapshot.TaxCents),
            documents.Where(v=>v.Snapshot.Kind==DocumentKind.CreditNote).Sum(v=>v.Snapshot.TaxCents),tax.Where(t=>t.Kind=="Receipt").Sum(t=>t.Cents),
            -tax.Where(t=>t.Kind=="Refund").Sum(t=>t.Cents),-tax.Where(t=>t.Kind=="TaxRemittance").Sum(t=>t.Cents),
            -trial.Where(r=>r.Account.Code=="2000").Sum(r=>r.Ending));
    }

    private string[] OpeningWarnings(DbWork db,OwnerSession owner,List<AccountTotal> trial)
    {
        var cash=trial.Where(r=>r.Account.Active && r.Account.CashKind!="None" && !r.Account.OpeningConfirmed).Select(r=>r.Account.Name+": opening unconfirmed");
        var loans=new RecordStore<Loan>(store,"Loan").List(db,owner).Where(l=>!l.Archived && l.OpeningPrincipalCents is null).Select(l=>l.Name+": opening principal unknown");
        var assets=new RecordStore<Asset>(store,"Asset").List(db,owner).Where(a=>!a.Archived && (a.CostCents is null || a.TaxBasisCents is null)).Select(a=>a.Name+": cost or tax basis unknown");
        return cash.Concat(loans).Concat(assets).ToArray();
    }

    private static HashSet<string> ConfirmedOpenings(DbWork db,OwnerSession owner,DateOnly through)=>db.Read("""
        SELECT AccountId FROM OpeningBalances O WHERE O.BusinessId=$business AND O.DimensionKind='Account' AND O.Date<=$date
        AND (O.RevokedAt IS NULL OR (O.ReversalMovementId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Movements R WHERE R.Id=O.ReversalMovementId AND R.Date<=$date)));
        """,r=>r.GetString(0),("$business",owner.BusinessId),("$date",through.ToString("yyyy-MM-dd"))).ToHashSet();
}
