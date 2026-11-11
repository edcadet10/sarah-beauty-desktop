namespace SarahBeauty.Business.Finance;

public sealed record CustomerPosition(string CustomerId,string Name,long OpenCents,long OverdueCents,long DueTodayCents,long FutureCents,
    long UnknownDueCents,long UnallocatedReceiptsCents,long UnusedCreditCents);
public sealed record CashActivity(string MovementId,DateOnly Date,string Kind,string Memo,string AccountId,string AccountName,long DebitCents,long CreditCents);
public sealed record TaxPosition(long ChargedCents,long ChargeReductionCents,long CollectedCents,long RefundedCents,long RemittedCents,long LiabilityCents);
public sealed record SupplierPosition(string SupplierId,string Name,long OpenCents,long OverdueCents,long DueTodayCents,long FutureCents,long UnknownDueCents,long CreditCents=0);
public sealed record ReportSnapshot(string BusinessName,string Mode,long Watermark,string GeneratedAtUtc,DateOnly From,DateOnly Through,
    List<AccountTotal> TrialBalance,ProfitAndLoss ProfitLoss,BalanceSheet BalanceSheet,List<CashActivity> Cash,List<CustomerPosition> Customers,
    TaxPosition Tax,string[] OpeningWarnings)
{
    public List<CashActivity> Entries {get;init;}=[];
    public List<SupplierPosition> Suppliers {get;init;}=[];
    public List<Purchasing.PurchaseDue> SupplierBills {get;init;}=[];
}
