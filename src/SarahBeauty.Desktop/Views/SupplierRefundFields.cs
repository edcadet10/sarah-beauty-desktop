using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class SupplierRefundFields(BusinessStore store,OwnerSession owner,FinancialDraft draft,ComboBox original,TextBox date)
{
    private readonly List<(string Id,TextBox Amount)> _amounts=[];
    private readonly StackPanel _lines=Form.Panel();
    private readonly CheckBox _specific=new(){Content="Choose specific bill line amounts"};

    internal UIElement Build()
    {
        _specific.IsChecked=draft.SupplierRefundPortions.Length>0;
        var panel=Form.Panel();
        var status=Form.Text();
        panel.Add(Form.Text("The default splits a refund across the original payment's remaining lines. Choose specific amounts when only selected items were refunded. Enter zero for other items; the selected amounts must equal the total refund."),
            _specific,Form.Action("Load refundable original lines",status,()=>
            {
                Load(draft.SupplierRefundPortions);
                return Task.CompletedTask;
            }),status,_lines);
        if(draft.SupplierRefundPortions.Length>0)
        {
            try{Load(draft.SupplierRefundPortions);}
            catch(ArgumentException error){status.Text=error.Message;}
        }
        original.SelectionChanged+=(_,_)=>{_amounts.Clear();_lines.Children.Clear();};
        return new Expander{Header="Supplier refund line amounts",Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch};
    }

    private void Load(SupplierRefundPortion[] saved)
    {
        var asOf=Input.Date(date.Text)??throw new ArgumentException("Enter the refund date before loading original lines.");
        var available=new PurchasePayments(store).RefundableLines(owner,original.SelectedId(),asOf);
        _amounts.Clear();_lines.Children.Clear();
        foreach(var line in available)
        {
            long cents=original.SelectedId()==draft.OriginalId?saved.SingleOrDefault(p=>p.LineId==line.LineId)?.GrossCents??0:0;
            var amount=Form.Input($"{line.Description} • {Money.Display(line.RemainingCents)} remaining • {line.LineId[..Math.Min(8,line.LineId.Length)]}",Money.Input(cents));
            _amounts.Add((line.LineId,amount));
            _lines.Add(amount);
        }
    }

    internal FinancialDraft Read(FinancialDraft value)
    {
        if(value.Kind!=FinancialKind.PurchaseRefund)return value;
        if(_specific.IsChecked!=true)return value with{SupplierRefundPortions=[]};
        var portions=_amounts.Select(a=>new SupplierRefundPortion(a.Id,Money.Parse(a.Amount.Text))).ToArray();
        if(portions.Any(p=>p.GrossCents<0) || !portions.Any(p=>p.GrossCents>0))
            throw new ArgumentException("Load the original lines and enter the amounts actually refunded.");
        return value with{SupplierRefundPortions=portions.Where(p=>p.GrossCents>0).ToArray()};
    }
}
