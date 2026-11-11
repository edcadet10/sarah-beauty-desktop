using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Dashboard;

public sealed class DashboardSources(BusinessStore store)
{
    internal Action? AfterFinanceRead {get;set;}
    public DashboardSnapshot Load(OwnerSession owner,DashboardRequest request)=>store.ReadSnapshot(db=>
    {
        store.RequireSession(db,owner);var finance=new DashboardFinanceSources(store).Load(db,owner,request.FinancialFrom,request.FinancialThrough);AfterFinanceRead?.Invoke();
        var upcoming=new DashboardUpcomingSources(store).Load(db,owner,request.PlanningAsOf,request.UpcomingFrom,request.UpcomingThrough);
        var operations=new DashboardOperationsSources(store).Load(db,owner,request.PlanningAsOf,finance);
        return new DashboardSnapshot(1,request,finance,upcoming,operations);
    });
}
