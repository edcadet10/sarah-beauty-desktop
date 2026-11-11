using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Purchasing;

public sealed record PurchaseCredit : BusinessRecord
{
    public string PurchaseId {get;init;}="";
    public string SupplierReference {get;init;}="";
    public bool ReliableReference {get;init;}
    public DateOnly? Date {get;init;}
    public string EvidenceId {get;init;}="";
    public PurchaseCreditLine[] Lines {get;init;}=[];
    public long PurchaseGeneration {get;init;}
    public string Status {get;init;}="Draft";
    public string ApprovedBy {get;init;}="";
    public string ApprovedAtUtc {get;init;}="";
    public string ReversesId {get;init;}="";
    public int Sign {get;init;}=1;
    public override string ToString()=>$"{Date:yyyy-MM-dd} • {Name} • {Status}";
}
public sealed record PurchaseCreditLine(string LineId,long Cents,long? TaxCents,long BusinessCents=0);
public sealed record PurchaseCreditRemaining(string LineId,string Description,long Cents,long TaxCents,long BusinessCents);
