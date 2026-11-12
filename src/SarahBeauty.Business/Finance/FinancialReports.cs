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
        var report=Read(db,owner.BusinessId,from,through,DateTimeOffset.UtcNow.ToString("O"),AfterTrialRead);
        store.RequireSession(db,owner);
        return report;
    }

    internal static ReportSnapshot Read(DbWork db,string businessId,DateOnly from,DateOnly through,string generatedAtUtc,Action? afterTrialRead=null)
    {
        var trial=LedgerReportKernel.TrialBalance(db,businessId,from,through);
        afterTrialRead?.Invoke();
        var pnl=LedgerReportKernel.ProfitLoss(trial);
        var balance=LedgerReportKernel.BalanceSheet(trial);
        string business=db.Scalar("SELECT DisplayName FROM Business WHERE Id=$id;",("$id",businessId)) as string??throw new ArgumentException("Choose a business present in the report source.");
        long watermark=Convert.ToInt64(db.Scalar("SELECT Watermark FROM Business WHERE Id=$id;",("$id",businessId)));
        var versions=DocumentVersions.List(db,businessId);
        var entries=Activity(db,businessId,from,through);
        var cashAccounts=trial.Where(r=>r.Account.CashKind!="None").Select(r=>r.Account.Id).ToHashSet();
        var bills=PurchasePayments.Outstanding(db,businessId,through);
        string mode=(string)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!;
        var debts=DebtSchedule.Read(db,businessId,from,through);
        return new ReportSnapshot(business,mode,watermark,generatedAtUtc,from,through,trial,pnl,balance,entries.Where(e=>cashAccounts.Contains(e.AccountId)).ToList(),
            CustomerPositions.Read(db,businessId,through,versions),TaxReportKernel.Read(db,businessId,from,through,trial,versions),OpeningWarnings(db,businessId,trial))
            {Entries=entries,SupplierBills=bills,Suppliers=SupplierPositions.Read(db,businessId,through,bills),Assets=AssetSchedule.Read(db,businessId,from,through),
                Debts=debts,Owners=OwnerSchedule.Read(db,businessId,from,through,debts),Bookings=BookingContributionReports.Read(db,businessId,from,through,versions,generatedAtUtc),
                TaxFilings=TaxFilingReports.Read(db,businessId,from,through)};
    }

    private static List<CashActivity> Activity(DbWork db,string businessId,DateOnly from,DateOnly through)=>db.Read("""
        SELECT M.Id,M.Date,M.Kind,M.Memo,A.Id,A.Name,J.DebitCents,J.CreditCents FROM JournalLines J JOIN Movements M ON M.Id=J.MovementId
        JOIN Accounts A ON A.Id=J.AccountId WHERE M.BusinessId=$business
        AND M.Date BETWEEN $from AND $through ORDER BY M.Date,M.Id,A.Code;
        """,r=>new CashActivity(r.GetString(0),DateOnly.Parse(r.GetString(1)),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetInt64(6),r.GetInt64(7)),
        ("$business",businessId),("$from",from.ToString("yyyy-MM-dd")),("$through",through.ToString("yyyy-MM-dd")));

    private static string[] OpeningWarnings(DbWork db,string businessId,List<AccountTotal> trial)
    {
        var cash=trial.Where(r=>r.Account.Active && r.Account.CashKind!="None" && !r.Account.OpeningConfirmed).Select(r=>r.Account.Name+": opening unconfirmed");
        var loans=RecordStore<Loan>.ReadBusiness(db,businessId,"Loan").Where(l=>!l.Archived && l.OpeningPrincipalCents is null).Select(l=>l.Name+": opening principal unknown");
        var assets=RecordStore<Asset>.ReadBusiness(db,businessId,"Asset").Where(a=>!a.Archived && (a.CostCents is null || a.TaxBasisCents is null)).Select(a=>a.Name+": cost or tax basis unknown");
        return cash.Concat(loans).Concat(assets).ToArray();
    }

}
