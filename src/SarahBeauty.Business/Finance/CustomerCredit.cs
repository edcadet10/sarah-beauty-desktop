using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

public enum CreditAction { Issue,ReturnToReceipt }
public sealed record CreditDraft : BusinessRecord
{
    public CreditAction Action {get;init;}
    public string PaymentId {get;init;}="";
    public string CreditId {get;init;}="";
    public long Cents {get;init;}
    public DateOnly? Date {get;init;}
    public long PaymentGeneration {get;init;}
    public long CreditGeneration {get;init;}
    public string Status {get;init;}="Draft";
}

public sealed record CustomerCredit(string Id,string CustomerId,string CustomerName,string PaymentId,
    DateOnly IssueDate,long IssuedCents,long IssuedTaxCents,string Reason);
public sealed record CreditPreview(string CustomerName,long Cents,long TaxCents,ValueBalance ReceiptAfter,ValueBalance CreditAfter);
