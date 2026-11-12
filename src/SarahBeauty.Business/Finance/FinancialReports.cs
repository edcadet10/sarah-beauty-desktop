using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

public sealed class FinancialReports(BusinessStore store)
{
    internal Action? AfterTrialRead {get;set;}
    public ReportSnapshot Load(OwnerSession owner,DateOnly from,DateOnly through)=>store.ReadSnapshot(db=>Load(db,owner,from,through));

    internal ReportSnapshot Load(DbWork db,OwnerSession owner,DateOnly from,DateOnly through)
    {
        store.RequireSession(db,owner);
        var trial=LedgerReports.TrialBalance(db,owner,from,through);
        AfterTrialRead?.Invoke();
        var pnl=LedgerReportKernel.ProfitLoss(trial);
        var balance=LedgerReportKernel.BalanceSheet(trial);
        string business=(string)db.Scalar("SELECT DisplayName FROM Business WHERE Id=$id;",("$id",owner.BusinessId))!;
        long watermark=ReceiptSnapshot.Watermark(db,owner);
        var versions=DocumentVersions.List(db,owner);
        var entries=Activity(db,owner,from,through);
        var cashAccounts=trial.Where(r=>r.Account.CashKind!="None").Select(r=>r.Account.Id).ToHashSet();
        var bills=PurchasePayments.Outstanding(db,store,owner,through);
        string mode=(string)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!;
        var debts=DebtSchedule.Read(db,store,owner,from,through);
        return new ReportSnapshot(business,mode,watermark,DateTimeOffset.UtcNow.ToString("O"),from,through,trial,pnl,balance,entries.Where(e=>cashAccounts.Contains(e.AccountId)).ToList(),
            CustomerPositions.Read(db,owner,through,versions),Tax(db,owner,from,through,trial,versions),OpeningWarnings(db,owner,trial))
            {Entries=entries,SupplierBills=bills,Suppliers=Finance.SupplierPositions.Read(db,owner.BusinessId,through,bills),Assets=AssetSchedule.Read(db,store,owner,from,through),
                Debts=debts,Owners=OwnerSchedule.Read(db,owner,from,through,debts),Bookings=BookingContributionReports.Read(db,store,owner,from,through,versions),
                TaxFilings=TaxFilingReports.Read(db,store,owner,from,through)};
    }

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
        var refunds=db.Read("""
            SELECT CASE WHEN M.Kind='Reversal' THEN -O.TaxCents ELSE M.TaxCents END FROM Movements M
            LEFT JOIN Movements O ON O.Id=M.ReversesId WHERE M.BusinessId=$business AND M.Date BETWEEN $from AND $through
            AND (M.Kind='Refund' OR (M.Kind='Reversal' AND O.Kind='Refund'));
            """,r=>r.GetInt64(0),("$business",owner.BusinessId),("$from",from.ToString("yyyy-MM-dd")),("$through",through.ToString("yyyy-MM-dd")));
        var recovery=TaxRecoveryActivity(db,owner,from,through);
        return new(documents.Where(v=>v.Snapshot.Kind==DocumentKind.Invoice && v.Status!="Void").Sum(v=>v.Snapshot.TaxCents),
            documents.Where(v=>v.Snapshot.Kind==DocumentKind.CreditNote).Sum(v=>v.Snapshot.TaxCents),tax.Where(t=>t.Kind is "Receipt" or "AssetDisposal" or "ReceiptClassification").Sum(t=>t.Cents),
            refunds.Sum(),-tax.Where(t=>t.Kind=="TaxRemittance").Sum(t=>t.Cents),
            -trial.Where(r=>r.Account.Code=="2000").Sum(r=>r.Ending))
        {
            RecoverableCents=trial.Where(r=>r.Account.Code=="1250").Sum(r=>r.Ending),
            RecoveredCashCents=recovery.Where(r=>r.Destination==TaxRecoveryDestination.Cash).Sum(r=>r.Cents),
            OffsetCents=recovery.Where(r=>r.Destination==TaxRecoveryDestination.Offset).Sum(r=>r.Cents),
            NonrecoverableCents=trial.Where(r=>r.Account.Code=="5050").Sum(r=>r.Debits-r.Credits)
        };
    }

    private static List<(string Destination,long Cents)> TaxRecoveryActivity(DbWork db,OwnerSession owner,DateOnly from,DateOnly through)=>db.Read("""
        SELECT json_extract(CASE WHEN M.Kind='Reversal' THEN O.DataJson ELSE M.DataJson END,'$.TaxRecoveryDestination'),
            CASE WHEN M.Kind='Reversal' THEN -O.GrossCents ELSE M.GrossCents END FROM Movements M LEFT JOIN Movements O ON O.Id=M.ReversesId
        WHERE M.BusinessId=$business AND M.Date BETWEEN $from AND $through AND (M.Kind='TaxRecovery' OR (M.Kind='Reversal' AND O.Kind='TaxRecovery'));
        """,r=>(r.GetString(0),r.GetInt64(1)),("$business",owner.BusinessId),("$from",from.ToString("yyyy-MM-dd")),("$through",through.ToString("yyyy-MM-dd")));

    private string[] OpeningWarnings(DbWork db,OwnerSession owner,List<AccountTotal> trial)
    {
        var cash=trial.Where(r=>r.Account.Active && r.Account.CashKind!="None" && !r.Account.OpeningConfirmed).Select(r=>r.Account.Name+": opening unconfirmed");
        var loans=new RecordStore<Loan>(store,"Loan").List(db,owner).Where(l=>!l.Archived && l.OpeningPrincipalCents is null).Select(l=>l.Name+": opening principal unknown");
        var assets=new RecordStore<Asset>(store,"Asset").List(db,owner).Where(a=>!a.Archived && (a.CostCents is null || a.TaxBasisCents is null)).Select(a=>a.Name+": cost or tax basis unknown");
        return cash.Concat(loans).Concat(assets).ToArray();
    }

}
