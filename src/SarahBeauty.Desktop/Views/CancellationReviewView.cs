using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class CancellationReviewView
{
    internal static UIElement Build(CancellationPreview review,List<Account> accounts)
    {
        var panel=Form.Panel();var draft=review.Decision;var terms=review.Before.Documents.SingleOrDefault(d=>d.Id==draft.TermsVersionId);
        panel.Add(Form.Text($"{review.Before.BusinessName} • {review.Before.Mode} • Reviewed watermark {review.Before.Watermark}\n{review.Before.BookingName}\nRequested {draft.RequestedDate:yyyy-MM-dd} • Effective {draft.Date:yyyy-MM-dd}\nDecision: {draft.Name}\nAgreement review: {draft.AgreementReview}\nEvidence: {draft.EvidenceId}"),
            Form.Text(terms is null?"No issued terms selected.":$"Reviewed terms: {terms}\nIssued {terms.Snapshot.IssueDate:yyyy-MM-dd}\n{terms.Snapshot.Terms}\nAcceptance evidence: {terms.AcceptanceEvidence??"Not recorded"}"),
            Form.Text($"Work status: {review.Before.BookingStatus} → {review.After.BookingStatus}\nRemaining invoice obligations: {Money.Display(review.After.Dues.Sum(d=>d.Cents))}. Unadjusted charges remain due."),
            PagedReport.Build("Cancellation actions",["Action","Original source","Amount","Tax / destination"],review.Actions.Select(a=>new[]{a.Kind,a.SourceId,Money.Display(a.Cents),
                a.Kind=="Retain source treatment"?"Existing treatment retained":Money.Display(a.TaxCents)+(a.DestinationBookingId.Length==0?"":" • Destination "+a.DestinationBookingId)})),
            PagedReport.Build("Cancellation journal",["Account","Debit","Credit"],review.Journal.Select(j=>new[]{accounts.Single(a=>a.Id==j.AccountId).ToString(),Money.Display(j.DebitCents),Money.Display(j.CreditCents)})),
            Section("Sources before this decision",Sources(review.Before)),Section("Sources after this decision",Sources(review.After)));
        return panel;
    }

    internal static UIElement Sources(CancellationSnapshot source)
    {
        var panel=Form.Panel();panel.Add(Form.Text($"Captured as of {source.AsOf:yyyy-MM-dd} • Watermark {source.Watermark}\nBook periods closed through {source.ClosedThrough?.ToString("yyyy-MM-dd")??"None"}. Later dated actions remain reserved."),
            PagedReport.Build("Cancellation receipt sources",["Payer / source","Original gross / tax","Available at date","Available after all dated actions"],source.Payments.Select(p=>new[]{
                $"{p.CustomerName} • {p.Kind}\n{p.Classification}\n{p.Id}"+(p.AdditionalCashReceipt?"":"\nNo additional cash receipt")+(p.CancelledOn.HasValue?$"\nPosting cancelled {p.CancelledOn:yyyy-MM-dd}":""),
                $"{Money.Display(p.GrossCents)} / {Money.Display(p.TaxCents)}",Money.Display(p.Available.Cents),Money.Display(p.AvailableAfterAllDatedActions.Cents)})),
            PagedReport.Build("Cancellation credit sources",["Payer / credit","Source treatment","Available at date","Available after all dated actions"],source.Credits.Select(c=>new[]{
                c.Source.CustomerName+" • "+c.Source.Id,c.Source.SourceTreatment,Money.Display(c.Available.Cents),Money.Display(c.AvailableAfterAllDatedActions.Cents)})),
            PagedReport.Build("Cancellation application sources",["Invoice / booking","Original application","Remaining at date","Remaining after all dated actions"],source.Applications.Select(a=>new[]{
                a.InvoiceNumber+"\n"+a.InvoiceBookingId+(a.InvoiceBookingId==source.BookingId?"":"\nAnother booking"),a.Id,Money.Display(a.Remaining.Cents),Money.Display(a.RemainingAfterAllDatedActions.Cents)})),
            PagedReport.Build("Cancellation invoice obligations",["Invoice / payer","Milestone / due date","Due","Included tax"],source.Dues.Select(d=>new[]{
                d.Number+" • "+d.CustomerName,d.Kind+" • "+(d.DueDate?.ToString("yyyy-MM-dd")??"Undecided"),Money.Display(d.Cents),Money.Display(d.TaxCents)})));
        return panel;
    }
    private static Expander Section(string label,UIElement content)=>new(){Header=label,Content=content,HorizontalAlignment=HorizontalAlignment.Stretch};
}
