using SarahBeauty.Business.Finance;

namespace SarahBeauty.Business.Dashboard;

public sealed record DashboardMonth(DateOnly Month,long? IncomeCents,long? ExpenseCents,long? ResultCents,bool HasPostedActivity,bool HasClosedCoverage);
public sealed record DashboardAgingBand(string Name,long Cents,InvoiceDue[] Sources);
public sealed record DashboardExpense(string AccountId,string Code,string Name,long Cents);
public sealed record DashboardCashAccount(Account Account,long BalanceCents,string ReconciliationId,DateOnly? ReconciledThrough,string ReconciliationHash);
public sealed record DashboardFinanceSnapshot(ReportSnapshot Reports,DashboardMonth[] Months,DashboardAgingBand[] Aging,DashboardExpense[] Expenses,
    DashboardCashAccount[] CashAccounts,CashActivity[] CashEntries);
