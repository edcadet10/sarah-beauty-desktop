using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Bookings;

public sealed record CancellationDraft : BusinessRecord
{
    public string BookingId {get;init;}="";
    public DateOnly? RequestedDate {get;init;}
    public DateOnly? Date {get;init;}
    public string TermsVersionId {get;init;}="";
    public string AgreementReview {get;init;}="";
    public string EvidenceId {get;init;}="";
    public string ClientReason {get;init;}="";
    public CancellationUnapply[] Unapplications {get;init;}=[];
    public CancellationReduction[] Reductions {get;init;}=[];
    public CancellationValueChoice[] Values {get;init;}=[];
    public string Status {get;init;}="Draft";
    public string ApprovedCaseId {get;init;}="";
}
public sealed record CancellationUnapply(string ApplicationId,long Cents);
public sealed record CancellationReduction(string InvoiceVersionId,InvoicePortion[] Portions);
public sealed record CancellationValueChoice(string PaymentId,string CreditId,long RetainCents,long RefundCents,long RefundTaxCents,long TransferCents,
    string DestinationBookingId="",string RefundCashAccountId="",string RefundMethod="",string RefundEvidenceId="",TaxRefundAllocation? TaxRefundAllocation=null,bool RefundTaxResolved=false);
public sealed record CancellationAction(string Kind,string SourceId,string ResultId,long Cents,long TaxCents,string DestinationBookingId="");
public sealed record CancellationPreview(CancellationDraft Decision,CancellationSnapshot Before,CancellationSnapshot After,CancellationAction[] Actions,JournalLine[] Journal);
