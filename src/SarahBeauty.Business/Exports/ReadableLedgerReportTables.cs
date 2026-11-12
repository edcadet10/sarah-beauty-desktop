using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Exports;

internal static class ReadableLedgerReportTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget,DateOnly from,DateOnly through)=>snapshot.Read(db=>
    {
        if(from>through)throw new ArgumentException("The report start must be on or before its end.");
        var source=snapshot.Source;var tables=new Tables(source,budget);
        var trial=LedgerReportKernel.TrialBalance(db,source.BusinessId,from,through);
        tables.Scope(source.GenerationId,"CapturedSource",null,from,through,source.Mode,source.Watermark,source.CapturedAtUtc);
        tables.Ledger(source.GenerationId,trial,LedgerReportKernel.ProfitLoss(trial),LedgerReportKernel.BalanceSheet(trial));
        tables.Cash(source.GenerationId,Cash(db,source,from,through));
        foreach(var account in trial.Where(r=>r.Account.Active&&r.Account.CashKind!="None"&&!r.Account.OpeningConfirmed))tables.Warning(source.GenerationId,"Cash opening unconfirmed",account.Account.Id,account.Account.Name);
        ReadableBookHistory.ForEachClosed(db,source,(id,period)=>
        {
            var report=period.Reports;
            tables.Scope(id,"RetainedPeriod",id,report.From,report.Through,report.Mode,report.Watermark,report.GeneratedAtUtc);
            tables.Ledger(id,report.TrialBalance,report.ProfitLoss,report.BalanceSheet);tables.Cash(id,report.Cash);
            foreach(string warning in report.OpeningWarnings)tables.Warning(id,"Retained opening warning",null,warning);
        });
        return tables.Freeze();
    });

    private static List<CashActivity> Cash(DbWork db,ReadableExportSource source,DateOnly from,DateOnly through)=>db.Read("""
        SELECT M.Id,M.Date,M.Kind,M.Memo,A.Id,A.Name,J.DebitCents,J.CreditCents
        FROM JournalLines J JOIN Movements M ON M.Id=J.MovementId JOIN Accounts A ON A.Id=J.AccountId
        WHERE M.BusinessId=$business AND A.BusinessId=$business AND A.CashKind<>'None' AND M.Date BETWEEN $from AND $through
        ORDER BY M.Date,M.Id,A.Code,J.Id;
        """,r=>new CashActivity(r.GetString(0),StoredDates.Parse(r.GetString(1)),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetInt64(6),r.GetInt64(7)),
        ("$business",source.BusinessId),("$from",from.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture)),("$through",through.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture)));

    private sealed class Tables
    {
        private readonly ReadableExportSource _source;
        private readonly ReadableTableBuilder _scopes,_trial,_profit,_balance,_cash,_warnings;
        internal Tables(ReadableExportSource source,ReadableExportBudget budget)
        {
            _source=source;
            _scopes=new(source,budget,"Ledger report scopes","ReportId","BusinessId","SourceKind","RetainedPeriodId","From","Through","ObservedMode","ObservedWatermark","GeneratedAtUtc");
            _trial=new(source,budget,"Report trial balance","ReportId","BusinessId","AccountId","Code","Name","Type","CashKind","ActiveAtCapture","OpeningConfirmedAsOf","OpeningCents","DebitCents","CreditCents","EndingCents");
            _profit=new(source,budget,"Report profit and loss","ReportId","BusinessId","IncomeCents","ExpenseCents","ResultCents");
            _balance=new(source,budget,"Report balance sheet","ReportId","BusinessId","AssetsCents","LiabilitiesCents","EquityCents","Balances");
            _cash=new(source,budget,"Report cash activity","ReportId","BusinessId","Sequence","MovementId","Date","Kind","Description","AccountId","AccountName","DebitCents","CreditCents");
            _warnings=new(source,budget,"Ledger report opening warnings","ReportId","BusinessId","Kind","AccountId","Description");
        }
        internal void Scope(string id,string kind,string? period,DateOnly from,DateOnly through,string mode,long watermark,string time)=>_scopes.Add(id,_source.BusinessId,kind,period,from,through,mode,watermark,time);
        internal void Ledger(string id,IEnumerable<AccountTotal> rows,ProfitAndLoss profit,BalanceSheet balance)
        {
            foreach(var row in rows.OrderBy(r=>r.Account.Code).ThenBy(r=>r.Account.Id))
            {
                var account=row.Account;
                _trial.Add(id,_source.BusinessId,account.Id,account.Code,account.Name,account.Type,account.CashKind,account.Active,account.OpeningConfirmed,row.Opening,row.Debits,row.Credits,row.Ending);
            }
            _profit.Add(id,_source.BusinessId,profit.IncomeCents,profit.ExpenseCents,profit.ResultCents);
            _balance.Add(id,_source.BusinessId,balance.AssetsCents,balance.LiabilitiesCents,balance.EquityCents,balance.Balances);
        }
        internal void Cash(string id,IEnumerable<CashActivity> rows)
        {
            int sequence=0;
            foreach(var row in rows)_cash.Add(id,_source.BusinessId,++sequence,row.MovementId,row.Date,row.Kind,row.Memo,row.AccountId,row.AccountName,row.DebitCents,row.CreditCents);
        }
        internal void Warning(string id,string kind,string? account,string description)=>_warnings.Add(id,_source.BusinessId,kind,account,description);
        internal ReadableExportTable[] Freeze()=>[_scopes.Freeze(),_trial.Freeze(),_profit.Freeze(),_balance.Freeze(),_cash.Freeze(),_warnings.Freeze()];
    }
}
