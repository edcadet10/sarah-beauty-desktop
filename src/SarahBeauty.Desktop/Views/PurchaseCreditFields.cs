using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class PurchaseCreditFields
{
    private readonly TextBox _reason,_date,_reference,_evidence;
    private readonly CheckBox _reliable;
    private readonly List<(string Id,TextBox Gross,TextBox Tax)> _lines=[];
    private readonly StackPanel _panel=Form.Panel();

    internal PurchaseCreditFields(PurchaseCredit value,List<PurchaseCreditRemaining> remaining)
    {
        _reason=Form.Input("Supplier reduction or correction reason",value.Name);
        _date=Form.Input("Credit / reversal date (YYYY-MM-DD)",value.Date?.ToString("yyyy-MM-dd")??"");
        _reference=Form.Input("Supplier credit / debit document reference (optional)",value.SupplierReference);
        _reliable=new CheckBox{Content="This reference uniquely identifies the supplier adjustment",IsChecked=value.ReliableReference};
        _evidence=Form.Input("Supplier document evidence reference (optional)",value.EvidenceId);
        _panel.Add(_reason,_date,_reference,_reliable,_evidence);
        if(value.ReversesId.Length>0){_panel.Add(Form.Text("This correction restores the exact original reduction. It does not edit the supplier document or move cash."));return;}
        foreach(var line in remaining)
        {
            var selected=value.Lines.SingleOrDefault(l=>l.LineId==line.LineId);
            var gross=Form.Input($"{line.Description} • Remaining gross {Money.Display(line.Cents)} • Credit gross in USD (zero leaves unchanged)",Money.Input(selected?.Cents??0));
            var tax=Form.Input("Included supplier tax credited in USD (blank means unreviewed)",selected?.TaxCents is long cents?Money.Input(cents):"");
            _lines.Add((line.LineId,gross,tax));
            _panel.Add(gross,tax);
        }
    }

    internal UIElement Build()=>_panel;
    internal PurchaseCredit Read(PurchaseCredit previous)=>previous with{Name=_reason.Text,Date=Input.Date(_date.Text),SupplierReference=_reference.Text,
        ReliableReference=_reliable.IsChecked==true,EvidenceId=_evidence.Text.Trim(),Lines=previous.ReversesId.Length>0?previous.Lines:
            _lines.Select(l=>new PurchaseCreditLine(l.Id,Money.Parse(l.Gross.Text),string.IsNullOrWhiteSpace(l.Tax.Text)?null:Money.Parse(l.Tax.Text))).Where(l=>l.Cents!=0).ToArray()};
}
