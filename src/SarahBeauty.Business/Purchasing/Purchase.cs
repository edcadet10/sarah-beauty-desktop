using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Purchasing;

public sealed record Purchase : BusinessRecord
{
    public string SupplierId {get;init;}="";
    public string SupplierReference {get;init;}="";
    public bool ReliableReference {get;init;}
    public DateOnly? PurchaseDate {get;init;}
    public DateOnly? ServiceDate {get;init;}
    public DateOnly? DueDate {get;init;}
    public string EvidenceId {get;init;}="";
    public PurchaseLine[] Lines {get;init;}=[];
    public string Status {get;init;}="Draft";
    public string ApprovedBy {get;init;}="";
    public string ApprovedAtUtc {get;init;}="";
    public override string ToString()=>$"{PurchaseDate:yyyy-MM-dd} • {Name} • {Status}";
}

public sealed record PurchaseLine(string Id,string Description,decimal Quantity,long UnitCostCents,long? TaxPaidCents,
    decimal? BusinessUseFraction,string AccountId,string AssetId="",string BookingId="")
{
    public long? GrossCents=>TaxPaidCents.HasValue?checked(Money.Multiply(Quantity,UnitCostCents)+TaxPaidCents.Value):null;
    public long? BusinessCents=>GrossCents.HasValue && BusinessUseFraction.HasValue?Money.Multiply(BusinessUseFraction.Value,GrossCents.Value):null;
    public override string ToString()=>$"{Description} • Qty {Quantity} • {(GrossCents.HasValue?Money.Display(GrossCents.Value):"Tax unknown")} • {(BusinessUseFraction.HasValue?$"Business use {BusinessUseFraction:P0}":"Use unknown")}";
}
