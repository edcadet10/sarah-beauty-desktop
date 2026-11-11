using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Dashboard;

internal static class DashboardMonthly
{
    internal static DashboardMonth[] Read(DbWork db,OwnerSession owner,ReportSnapshot reports,int count)
    {
        var closed=db.Read("""
            SELECT C.FromDate,C.ThroughDate,C.SnapshotJson,C.SnapshotHash FROM BookPeriodCloses C WHERE C.BusinessId=$business
            AND C.FromDate<=$through AND C.ThroughDate>=$from
            AND NOT EXISTS(SELECT 1 FROM BookPeriodReopens R WHERE R.PeriodId=C.Id) ORDER BY C.FromDate;
            """,r=>
            {
                if(BookPeriodQueries.Hash(r.GetString(2))!=r.GetString(3))throw new InvalidDataException("A closed report snapshot failed its content-hash check.");
                return (From:DateOnly.Parse(r.GetString(0)),Through:DateOnly.Parse(r.GetString(1)));
            },("$business",owner.BusinessId),("$from",reports.From.ToString("yyyy-MM-dd")),("$through",reports.Through.ToString("yyyy-MM-dd")));
        var accounts=reports.TrialBalance.ToDictionary(t=>t.Account.Id,t=>t.Account.Type);
        var groups=reports.Entries.ToLookup(e=>new DateOnly(e.Date.Year,e.Date.Month,1));
        DateOnly first=new(reports.From.Year,reports.From.Month,1);
        return Enumerable.Range(0,count).Select(i=>Month(first.AddMonths(i),groups,accounts,closed,reports)).ToArray();
    }

    private static DashboardMonth Month(DateOnly month,ILookup<DateOnly,CashActivity> entries,IReadOnlyDictionary<string,string> accounts,
        List<(DateOnly From,DateOnly Through)> closed,ReportSnapshot reports)
    {
        var rows=entries[month].ToArray();DateOnly last=new(month.Year,month.Month,DateTime.DaysInMonth(month.Year,month.Month));
        bool covered=Covers(month<reports.From?reports.From:month,last>reports.Through?reports.Through:last,closed);
        if(rows.Length==0 && !covered)return new(month,null,null,null,false,false);
        long income=rows.Where(e=>accounts[e.AccountId]=="Income").Sum(e=>e.CreditCents-e.DebitCents);
        long expenses=rows.Where(e=>accounts[e.AccountId]=="Expense").Sum(e=>e.DebitCents-e.CreditCents);
        return new(month,income,expenses,checked(income-expenses),rows.Length>0,covered);
    }

    private static bool Covers(DateOnly from,DateOnly through,List<(DateOnly From,DateOnly Through)> periods)
    {
        var next=from;
        foreach(var period in periods)
        {
            if(period.Through<next)continue;
            if(period.From>next)return false;
            if(period.Through>=through)return true;
            next=period.Through.AddDays(1);
        }
        return false;
    }
}
