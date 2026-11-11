using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class StatementImportFields(List<Account> accounts)
{
    private readonly ComboBox _account=Form.Records("Statement account",accounts.Where(a=>a.Active && a.CashKind!="None").Select(a=>(a.Id,a.ToString())));
    private readonly TextBox _from=Form.Input("Statement period start (YYYY-MM-DD)"),_through=Form.Input("Statement period end (YYYY-MM-DD)");
    private readonly TextBox _opening=Form.Input("Confirmed statement opening balance in USD"),_closing=Form.Input("Confirmed statement closing balance in USD");

    internal UIElement Build()
    {
        var panel=Form.Panel();
        panel.Add(_account,_from,_through,_opening,_closing);
        return new Expander{Header="Statement account, period, and balances",Content=panel,IsExpanded=true,HorizontalAlignment=HorizontalAlignment.Stretch};
    }

    internal StatementDraft Read(StatementSource source)=>new(_account.SelectedId(),Input.Date(_from.Text)??throw new ArgumentException("Enter the statement start date."),
        Input.Date(_through.Text)??throw new ArgumentException("Enter the statement end date."),Money.Parse(_opening.Text),Money.Parse(_closing.Text),source);
}
