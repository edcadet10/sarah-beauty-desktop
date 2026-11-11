using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class SupplierPaymentFields
{
    private readonly ComboBox _bill,_funding,_destination,_owner;
    private readonly FinancialKind _kind;

    internal SupplierPaymentFields(BusinessStore store,OwnerSession owner,FinancialDraft draft,ComboBox cash,ComboBox economicOwner)
    {
        (_kind,_owner)=(draft.Kind,economicOwner);
        _bill=Form.Records("Approved supplier bill",new PurchaseBook(store).List(owner).Where(p=>p.Status=="Approved").Select(p=>(p.Id,p.ToString())),draft.PurchaseId);
        _funding=Form.Select("Actual payment funding",["Business cash","Owner contribution","Owner reimbursable"],draft.PurchaseFunding);
        _destination=Form.Select("Actual refund destination and treatment",["Business cash","Reverse owner contribution","Reduce owner payable","Owner withdrawal"],draft.SupplierRefundDestination);
        void ShowCash()
        {
            if(_kind is not (FinancialKind.PurchasePayment or FinancialKind.PurchaseRefund))return;
            string selected=(string)(_kind==FinancialKind.PurchasePayment?_funding.SelectedItem:_destination.SelectedItem);
            cash.Visibility=selected=="Business cash"?Visibility.Visible:Visibility.Collapsed;
        }
        _funding.SelectionChanged+=(_,_)=>ShowCash();
        _destination.SelectionChanged+=(_,_)=>ShowCash();
        ShowCash();
    }

    internal UIElement Build()
    {
        var panel=Form.Panel();
        if(_kind==FinancialKind.PurchasePayment)panel.Add(_bill,_funding,_owner,
            Form.Text("The bill's reviewed business shares determine expense and asset entries. Business cash paying a personal share requires its benefiting owner. Owner-funded personal costs stay outside the business books."));
        else panel.Add(_destination,_owner,Form.Text("Choose the original approved supplier payment below. The refund restores its original cost shares. A cash refund reopens that bill amount until a separate bill reduction is approved. Choose the actual recipient and owner treatment."));
        return panel;
    }

    internal FinancialDraft Read(FinancialDraft draft)=>draft with
    {
        PurchaseId=_kind==FinancialKind.PurchasePayment?_bill.SelectedId():draft.PurchaseId,
        PurchaseFunding=_kind==FinancialKind.PurchasePayment?(string)_funding.SelectedItem:draft.PurchaseFunding,
        SupplierRefundDestination=_kind==FinancialKind.PurchaseRefund?(string)_destination.SelectedItem:draft.SupplierRefundDestination
    };
}
