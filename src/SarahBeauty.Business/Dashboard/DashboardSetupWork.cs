using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Dashboard;

internal static class DashboardSetupWork
{
    internal static IEnumerable<DashboardWorkItem> Read(DbWork db,BusinessStore store,OwnerSession owner,DateOnly asOf,DashboardFinanceSnapshot finance)
    {
        var items=new List<DashboardWorkItem>();
        foreach(var cash in finance.CashAccounts.Where(c=>c.Account.Active && !c.Account.OpeningConfirmed))
            items.Add(new("Opening review",cash.Account.Id,cash.Account.Name,"Opening balances",0,[$"Opening unconfirmed for the financial as-of date {finance.Reports.Through:yyyy-MM-dd}."]));
        if(finance.CashAccounts.Length==0)items.Add(new("Account setup","cash-setup","Cash, bank, and processor accounts","Money",0,["No cash accounts are registered."]));
        foreach(var asset in new RecordStore<Asset>(store,"Asset").List(db,owner).Where(a=>!a.Archived))
        {
            var reasons=new List<string>();if(asset.CostCents is null)reasons.Add("Registered cost unknown.");if(asset.TaxBasisCents is null)reasons.Add("Tax basis unconfirmed.");
            if(asset.BusinessUseFraction is null)reasons.Add("Business-use fraction unconfirmed.");
            if(reasons.Count>0)items.Add(new("Asset setup",asset.Id,asset.Name,"Resources",asset.Revision,reasons.ToArray()));
        }
        foreach(var loan in new RecordStore<Loan>(store,"Loan").List(db,owner).Where(l=>!l.Archived && l.OpeningPrincipalCents is null))
            items.Add(new("Loan setup",loan.Id,loan.Name,"Resources",loan.Revision,["Opening principal unknown."]));
        foreach(var service in new RecordStore<ServiceDefinition>(store,"Service").List(db,owner).Where(s=>!s.Archived && (s.UnitCents is null || s.TaxReviewStatus!="Reviewed")))
            items.Add(new("Service setup",service.Id,service.Name,"Service catalog",service.Revision,
                new[]{service.UnitCents is null?"Price unknown.":"",service.TaxReviewStatus!="Reviewed"?"Service tax treatment needs review.":""}.Where(s=>s.Length>0).ToArray()));
        items.AddRange(TaxPeriods(db,store,owner,asOf));return items;
    }

    private static IEnumerable<DashboardWorkItem> TaxPeriods(DbWork db,BusinessStore store,OwnerSession owner,DateOnly asOf)
    {
        var periods=new RecordStore<TaxPeriod>(store,"TaxPeriod").List(db,owner);
        if(periods.Count==0)return [new("Tax setup","tax-period-setup","Tax reporting periods","Tax periods",0,["No confirmed reporting periods are registered."])];
        return periods.Where(p=>!p.Archived && p.From<=asOf && p.Status is not ("Filed" or "Amended" or "Not required"))
            .Select(p=>new DashboardWorkItem("Tax review",p.Id,p.Name+" • "+p.Jurisdiction,"Tax periods",p.Revision,
                p.DueDate is null?["Filing status: "+p.Status,"Filing due date unconfirmed."]:["Filing status: "+p.Status,$"Recorded due date: {p.DueDate:yyyy-MM-dd}."]));
    }
}
