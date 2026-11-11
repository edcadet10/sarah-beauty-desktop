using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

public sealed record PaymentApplicationDraft : BusinessRecord
{
    public string PaymentId {get;init;}="";
    public string CreditId {get;init;}="";
    public string InvoiceVersionId {get;init;}="";
    public string MilestoneId {get;init;}="";
    public long Cents {get;init;}
    public DateOnly? Date {get;init;}
    public string OnBehalfReason {get;init;}="";
    public string ReversesId {get;init;}="";
    public long SourceGeneration {get;init;}
    public long InvoiceGeneration {get;init;}
    public string Status {get;init;}="Draft";
    public string GroupId {get;init;}="";
}

public sealed record ValueBalance(long Cents,long TaxCents);
public sealed record InvoiceDue(string InvoiceVersionId,string Number,string CustomerId,string CustomerName,
    string MilestoneId,string Kind,DateOnly? DueDate,long Cents,long TaxCents);
public sealed record PaymentApplication(string Id,string GroupId,string? PaymentId,string? CreditId,
    string InvoiceVersionId,string MilestoneId,long Cents,long TaxCents,DateOnly Date,string? ReversesId);
