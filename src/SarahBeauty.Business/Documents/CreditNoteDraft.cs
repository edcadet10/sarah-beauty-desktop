using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

public sealed record CreditNoteDraft : BusinessRecord
{
    public string InvoiceVersionId {get;init;}="";
    public DateOnly? IssueDate {get;init;}
    public InvoicePortion[] Portions {get;init;}=[];
    public long InvoiceGeneration {get;init;}
    public string Status {get;init;}="Draft";
    public string IssuedVersionId {get;init;}="";
}

internal sealed record MilestoneReduction(string MilestoneId,string Kind,long Cents,long TaxCents);
