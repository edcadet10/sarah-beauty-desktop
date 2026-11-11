using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Business.Bookings;

public sealed record CancellationPayment(string Id,FinancialKind Kind,string BookingId,string OriginalId,string CustomerId,string CustomerName,
    DateOnly Date,long GrossCents,long TaxCents,ValueBalance Available,ValueBalance AvailableAfterAllDatedActions,string Classification,string EvidenceId,DateOnly? CancelledOn)
{
    public bool AdditionalCashReceipt=>Kind!=FinancialKind.ReceiptClassification;
}
public sealed record CancellationCredit(CustomerCredit Source,ValueBalance Available,ValueBalance AvailableAfterAllDatedActions);
public sealed record CancellationApplication(string Id,string PaymentId,string CreditId,string InvoiceVersionId,string InvoiceNumber,string InvoiceBookingId,
    string MilestoneId,DateOnly Date,long OriginalCents,ValueBalance Remaining,ValueBalance RemainingAfterAllDatedActions);
public sealed record CancellationCharge(string InvoiceVersionId,ClientLine Source,decimal RemainingQuantity,decimal RemainingQuantityAfterAllDatedActions);
public sealed record CancellationSnapshot(string BusinessName,string Mode,long Watermark,string GeneratedAtUtc,DateOnly AsOf,
    string BookingId,int BookingRevision,string BookingName,string BookingStatus,ClientEvent Event,string AcceptedQuoteVersionId,DateOnly? ClosedThrough,
    DocumentVersion[] Documents,CancellationPayment[] Payments,CancellationCredit[] Credits,CancellationApplication[] Applications,InvoiceDue[] Dues,CancellationCharge[] Charges);
