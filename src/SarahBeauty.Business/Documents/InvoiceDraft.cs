using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

public sealed record InvoicePortion(string ChargeId,decimal Quantity);
public sealed record InvoiceDraft : BusinessRecord
{
    public string QuoteVersionId {get;init;}="";
    public string PayerCustomerId {get;init;}="";
    public DateOnly? IssueDate {get;init;}
    public DateOnly? DueDate {get;init;}
    public string Terms {get;init;}="";
    public InvoicePortion[] Portions {get;init;}=[];
    public long CoverageGeneration {get;init;}
    public string Status {get;init;}="Draft";
    public string IssuedVersionId {get;init;}="";
}

public sealed record ChargeRemaining(string ChargeId,string Description,decimal Quantity,long NetCents,long TaxCents);
internal sealed record CoveredPortion(decimal Quantity,long NetCents,long TaxCents,long DiscountCents,string Signature);
