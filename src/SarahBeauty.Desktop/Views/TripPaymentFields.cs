using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class TripPaymentFields
{
    private readonly SearchableRecords _payment=new("Approved trip reimbursement payment");
    private readonly TextBox _date,_amount,_payee,_evidence;
    private readonly string _trip;
    internal TripPaymentFields(TripReimbursementReview review,List<PostedMovement> payments)
    {
        _trip=review.CurrentTrip.Id;
        _payment.Update(payments.Where(p=>p.Source.Kind is FinancialKind.Expense or FinancialKind.OwnerReimbursement).Select(p=>(p.Id,
            $"{p.Source.Date:yyyy-MM-dd} • {p.Source.Kind} • {Money.Display(p.Source.AmountCents)} • {p.Source.Name} • {p.Id[..8]}")));
        _date=Form.Input("Trip payment link date (YYYY-MM-DD)",review.AsOf.ToString("yyyy-MM-dd"));
        _amount=Form.Input("Trip reimbursement amount to link in USD",review.CurrentTrip.RequestedReimbursementCents.HasValue?
            Money.Input(review.CurrentTrip.RequestedReimbursementCents.Value-review.CurrentClaimLinkedCents):"");
        _payee=Form.Input("Confirm the actual payment paid this trip's identified driver");_evidence=Form.Input("Trip payment link evidence");
    }
    internal UIElement Build()
    {
        var panel=Form.Panel();panel.Add(Form.Text("Approve the actual expense or owner reimbursement in Money first, then select its source here. The reviewed link uses saved trip details and creates no additional expense or payment."),
            _payment.Build(),_date,_amount,_payee,_evidence);return panel;
    }
    internal TripPaymentDraft Read()=>new(_trip,_payment.SelectedId,Input.Date(_date.Text)??throw new ArgumentException("Enter the trip payment link date."),
        Money.Parse(_amount.Text),_payee.Text,_evidence.Text);
}
