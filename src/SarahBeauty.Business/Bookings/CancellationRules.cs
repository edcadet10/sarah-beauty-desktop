using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Bookings;

internal static class CancellationRules
{
    internal static void Validate(DbWork db,BusinessStore store,OwnerSession owner,CancellationDraft draft,CancellationSnapshot sources)
    {
        if(draft.Status!="Draft" || draft.ApprovedCaseId.Length>0 || draft.RequestedDate is null || draft.Date is null || draft.RequestedDate>draft.Date
            || string.IsNullOrWhiteSpace(draft.Name) || string.IsNullOrWhiteSpace(draft.AgreementReview) || string.IsNullOrWhiteSpace(draft.EvidenceId))
            throw new ArgumentException("Record the cancellation request date, decision date, reason, agreement review, and evidence.");
        if(draft.TermsVersionId.Length>0 && !sources.Documents.Any(d=>d.Id==draft.TermsVersionId && d.Snapshot.Kind is DocumentKind.Quote or DocumentKind.Invoice
            && d.Status!="Void" && d.Snapshot.IssueDate<=draft.Date))throw new ArgumentException("Choose the actual issued terms from this booking on or before the decision date.");
        if(draft.Reductions.Length>0 && string.IsNullOrWhiteSpace(draft.ClientReason))throw new ArgumentException("Record a client-facing reason for the selected charge reductions.");
        Distinct(draft.Unapplications.Select(a=>a.ApplicationId),"application");Distinct(draft.Reductions.Select(r=>r.InvoiceVersionId),"invoice");
        Distinct(draft.Values.Select(v=>v.PaymentId+":"+v.CreditId),"value source");
        foreach(var value in draft.Values)Value(db,store,owner,sources,value);
        if(draft.Unapplications.Length>0 || draft.Reductions.Length>0 || draft.Values.Any(v=>v.RefundCents>0 || v.TransferCents>0))
            PostingValidation.CheckClosedPeriod(db,owner,draft.Date.Value);
    }

    private static void Distinct(IEnumerable<string> ids,string kind)
    {
        var all=ids.ToArray();if(all.Any(string.IsNullOrWhiteSpace) || all.Distinct().Count()!=all.Length)throw new ArgumentException($"Choose distinct original {kind} records.");
    }

    private static void Value(DbWork db,BusinessStore store,OwnerSession owner,CancellationSnapshot sources,CancellationValueChoice value)
    {
        if(!sources.Payments.Any(p=>p.Id==value.PaymentId) || value.CreditId.Length>0 && !sources.Credits.Any(c=>c.Source.Id==value.CreditId && c.Source.PaymentId==value.PaymentId))
            throw new ArgumentException("Choose a receipt or funded credit related to this booking.");
        if(value.RetainCents<0 || value.RefundCents<0 || value.TransferCents<0 || value.RefundTaxCents<0 || value.RefundTaxCents>value.RefundCents
            || checked(value.RetainCents+value.RefundCents+value.TransferCents)==0)throw new ArgumentException("Record positive reviewed value with valid retention, refund, transfer, and tax portions.");
        if(value.RefundCents>0 && (!value.RefundTaxResolved || string.IsNullOrWhiteSpace(value.RefundEvidenceId) || string.IsNullOrWhiteSpace(value.RefundMethod)))
            throw new ArgumentException("An actual refund needs its resolved tax treatment, method, and evidence.");
        if(value.TransferCents==0 && value.DestinationBookingId.Length>0)throw new ArgumentException("A destination booking belongs to a transferred credit share.");
        if(value.DestinationBookingId.Length>0)
        {
            var destination=new RecordStore<Booking>(store,"Booking").Get(db,owner,value.DestinationBookingId);
            if(destination.Id==sources.BookingId || destination.Status=="Cancelled" || destination.Archived)throw new ArgumentException("Choose a different active destination booking.");
        }
    }

    internal static void Limit(CancellationSnapshot sources,CancellationValueChoice value)
    {
        var available=value.CreditId.Length==0?sources.Payments.Single(p=>p.Id==value.PaymentId).Available:sources.Credits.Single(c=>c.Source.Id==value.CreditId).Available;
        long applied=sources.Applications.Where(a=>a.InvoiceBookingId==sources.BookingId && (value.CreditId.Length==0?a.PaymentId==value.PaymentId && a.CreditId.Length==0:a.CreditId==value.CreditId)).Sum(a=>a.Remaining.Cents);
        if(checked(value.RefundCents+value.TransferCents)>available.Cents || checked(value.RetainCents+value.RefundCents+value.TransferCents)>checked(available.Cents+applied))
            throw new ArgumentException("The combined decision exceeds this source's available value and its retained applications to this booking.");
    }
}
