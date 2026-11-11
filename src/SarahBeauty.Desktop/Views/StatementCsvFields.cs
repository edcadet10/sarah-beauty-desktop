using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Banking;

namespace SarahBeauty_Desktop.Views;

internal sealed class StatementCsvFields
{
    private readonly TextBox _date=Form.Input("Date column header","Date"),_description=Form.Input("Description column header","Description");
    private readonly TextBox _amount=Form.Input("Signed amount column header (blank for separate debit/credit)","Amount");
    private readonly TextBox _debit=Form.Input("Debit column header (separate columns only)"),_credit=Form.Input("Credit column header (separate columns only)");
    private readonly TextBox _reference=Form.Input("Reference column header (optional)");
    private readonly ComboBox _format=Form.Select("Exact date format",StatementCsv.DateFormats.ToArray(),"yyyy-MM-dd");
    private readonly ComboBox _delimiter=Form.Select("CSV delimiter",["Comma","Semicolon","Tab"],"Comma");
    private readonly CheckBox _invert=new(){Content="The signed amount column uses positive amounts for money leaving the account"};
    private readonly CheckBox _reliable=new(){Content="The bank reference uniquely identifies each real account transaction"};

    internal UIElement Build()
    {
        var panel=Form.Panel();
        panel.Add(_date,_description,_amount,_debit,_credit,_reference,_format,_delimiter,_invert,_reliable);
        return new Expander{Header="Map statement columns and signs",Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch,IsExpanded=true};
    }

    internal StatementCsvMapping Read()=>new(_date.Text.Trim(),_description.Text.Trim(),_amount.Text.Trim(),_debit.Text.Trim(),_credit.Text.Trim(),_reference.Text.Trim(),
        (string)_format.SelectedItem,(_delimiter.SelectedItem as string) switch{"Semicolon"=>";","Tab"=>"\t",_=>","},_invert.IsChecked==true,_reliable.IsChecked==true);
}
