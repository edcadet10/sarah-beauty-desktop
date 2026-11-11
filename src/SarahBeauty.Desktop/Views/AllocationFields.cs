using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class AllocationFields
{
    private readonly TextBox _name,_date,_amount,_behalf;
    private readonly ComboBox _payment,_invoice,_milestone,_reversal;
    private readonly StackPanel _panel=Form.Panel();

    internal AllocationFields(BusinessStore store,OwnerSession owner,PaymentApplicationDraft draft)
    {
        _name=Form.Input("Reason / reference",draft.Name);
        _date=Form.Input("Application or reversal date",draft.Date?.ToString("yyyy-MM-dd")??"");
        _amount=Form.Input("Amount in USD",Money.Input(draft.Cents));
        var payments=new FinanceBook(store).Posted(owner).Where(p=>p.Source.Kind==FinancialKind.Receipt).ToList();
        _payment=Form.Records("Approved receipt",payments.Select(p=>(p.Id,$"{p.Source} • {p.Id[..8]}")),draft.PaymentId);
        var invoices=new QuoteBook(store).Versions(owner).Where(v=>v.Snapshot.Kind==DocumentKind.Invoice && v.Status!="Void");
        _invoice=Form.Records("Payer invoice",invoices.Select(v=>(v.Id,v.ToString())),draft.InvoiceVersionId);
        var dues=new PaymentApplications(store).Outstanding(owner,draft.Date??DateOnly.FromDateTime(DateTime.Today));
        _milestone=Form.Records("Milestone (optional)",dues.Where(d=>d.Cents>0).Select(d=>(d.MilestoneId,$"{d.Number} • {d.Kind} • {Money.Display(d.Cents)}")),draft.MilestoneId,
            "Automatically fill trial, then deposit, then final");
        _behalf=Form.Input("Explicit on-behalf decision (when payer differs)",draft.OnBehalfReason);
        _reversal=Form.Records("Original application to reverse (leave blank for a new application)",new PaymentApplications(store).History(owner)
            .Where(a=>a.ReversesId is null).Select(a=>(a.Id,$"{a.Date:yyyy-MM-dd} • {Money.Display(a.Cents)} • {a.Id[..8]}")),draft.ReversesId);
        _panel.Add(_name,_date,_amount,_payment,_invoice,_milestone,_behalf,_reversal);
    }

    internal UIElement Build()=>_panel;
    internal PaymentApplicationDraft Read(PaymentApplicationDraft previous)=>previous with
    {
        Name=_name.Text,Date=Input.Date(_date.Text),Cents=Money.Parse(_amount.Text),PaymentId=_payment.SelectedId(),
        InvoiceVersionId=_invoice.SelectedId(),MilestoneId=_milestone.SelectedId(),OnBehalfReason=_behalf.Text.Trim(),ReversesId=_reversal.SelectedId()
    };
}
