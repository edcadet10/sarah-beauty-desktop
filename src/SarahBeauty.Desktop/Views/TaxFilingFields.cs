using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class TaxFilingFields
{
    private readonly ComboBox _status=Form.Select("Reviewed filing status",["Review required","Not filed","Filed","Amended","Not required"],"Review required");
    private readonly TextBox _date=Form.Input("Actual filing date (YYYY-MM-DD; filed/amended only)");
    private readonly TextBox _amount=Form.Input("Tax amount on the filed return in USD (blank if no filing; negative for a reported refund)");
    private readonly TextBox _reference=Form.Input("Actual filing reference (filed/amended only)");
    private readonly TextBox _evidence=Form.Input("Evidence reference");
    private readonly TextBox _reason=Form.Input("Reason for this status decision");
    private readonly string _request=Guid.NewGuid().ToString();

    internal UIElement Build()
    {
        var panel=Form.Panel();
        panel.Add(_status,_date,_amount,_reference,_evidence,_reason);
        return panel;
    }

    internal TaxFilingDecision Read(int revision)=>new(_request,revision,(string)_status.SelectedItem,Input.Date(_date.Text),
        string.IsNullOrWhiteSpace(_amount.Text)?null:Money.Parse(_amount.Text),_reference.Text,_evidence.Text,_reason.Text);
}
