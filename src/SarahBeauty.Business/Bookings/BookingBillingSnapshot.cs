using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Business.Bookings;

public sealed record BookingBillingInvoice(string VersionId,string Number,string SourceQuoteVersionId,string PayerId,string PayerName,
    DateOnly IssueDate,DateOnly? DueDate,long OriginalNetCents,long OriginalTaxCents,long ReducedNetCents,long ReducedTaxCents,
    long AppliedCents,long AppliedTaxCents,long OpenCents,long OpenTaxCents,string SourceHash,ClientLine[] Lines)
{
    public long EffectiveNetCents=>checked(OriginalNetCents-ReducedNetCents);
    public long EffectiveTaxCents=>checked(OriginalTaxCents-ReducedTaxCents);
}

public sealed record BookingBillingCredit(string VersionId,string InvoiceVersionId,string Number,DateOnly Date,long NetCents,long TaxCents,
    string SourceHash,ClientLine[] Lines);

public sealed record BookingBillingSnapshot(string BusinessName,string Mode,long Watermark,string GeneratedAtUtc,DateOnly AsOf,
    string BookingId,string CurrentBookingName,int CurrentBookingRevision,BookingBillingInvoice[] Invoices,BookingBillingCredit[] Credits,InvoiceDue[] Dues);
