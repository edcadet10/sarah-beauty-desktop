namespace SarahBeauty.Business.Dashboard;

public sealed record DashboardWorkItem(string Kind,string RecordId,string Name,string Route,int Revision,string[] Reasons,long? RelatedCents=null);
public sealed record DashboardSupply(string Id,string Name,string Brand,string Shade,string Status,string Location,DateOnly? ExpiryDate,bool Low,bool Finished,bool ExpiryAlert);
public sealed record DashboardOperationsSnapshot(string BusinessName,string Mode,long Watermark,string GeneratedAtUtc,DateOnly BusinessDate,
    DashboardWorkItem[] Work,DashboardSupply[] Supplies);
public sealed record DashboardRequest(DateOnly FinancialFrom,DateOnly FinancialThrough,DateOnly PlanningAsOf,DateOnly UpcomingFrom,DateOnly UpcomingThrough);
public sealed record DashboardSnapshot(int Version,DashboardRequest Request,DashboardFinanceSnapshot Finance,DashboardUpcomingSnapshot Upcoming,DashboardOperationsSnapshot Operations);
