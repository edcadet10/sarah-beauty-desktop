using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

public enum DocumentKind { Quote, Invoice, Receipt, CreditNote }

public sealed record DocumentDraft : BusinessRecord
{
    public DocumentKind Kind { get; init; } = DocumentKind.Quote;
    public string DocumentId { get; init; } = "";
    public string BookingId { get; init; } = "";
    public int BookingRevision { get; init; }
    public string RecipientCustomerId { get; init; } = "";
    public DateOnly? IssueDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public string Terms { get; init; } = "";
    public string Status { get; init; } = "Draft";
    public string IssuedVersionId { get; init; } = "";
}

public sealed record ClientParty(string Name,string BillingName,string BillingAddress,string Phone,string Email);
public sealed record ClientEvent(string Name,DateOnly? Date,string Venue,string ReadyBy);
public sealed record ClientLine(string ChargeId,string Description,string Unit,decimal Quantity,long UnitCents,
    long DiscountCents,long NetCents,long TaxCents,long TotalCents,long RoundingCents=0);
public sealed record ClientMilestone(string Kind,long Cents,DateOnly? DueDate);
public sealed record ClientSnapshot(DocumentKind Kind,string Number,int Version,string Mode,string BusinessName,
    ClientParty Client,ClientEvent Event,DateOnly IssueDate,DateOnly? DueDate,string Terms,ClientLine[] Lines,
    long NetCents,long TaxCents,long TotalCents,ClientMilestone[] Milestones)
{
    public string RelatedDocument {get;init;}="";
    public ClientReceipt? Receipt {get;init;}
}
public sealed record ClientReceiptApplication(string InvoiceNumber,string Milestone,long Cents);
public sealed record ClientReceipt(DateOnly PaymentDate,string Method,string Reference,ClientReceiptApplication[] Applications,
    long RefundCents,long PaymentAvailableCents,long UnusedFundedCreditCents,long CustomerInvoiceBalanceCents);
public sealed record DocumentVersion(string Id,string DocumentId,string BookingId,ClientSnapshot Snapshot,string Hash,string Status,
    DateOnly? AcceptedDate,string? AcceptanceEvidence)
{
    public override string ToString()=>$"{Snapshot.Number} v{Snapshot.Version} • {Snapshot.Client.Name} • {Status}";
}
