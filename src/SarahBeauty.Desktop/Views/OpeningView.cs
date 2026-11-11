using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class OpeningView(BusinessStore store,OwnerSession owner)
{
    private readonly OpeningBalances _book=new(store);
    private readonly ListView _list=new(){MaxHeight=240};
    private readonly StackPanel _detail=Form.Panel();
    private readonly TextBlock _status=Form.Text();

    internal UIElement Build()
    {
        var chart=new ChartOfAccounts(store);
        chart.Initialize(owner);
        var panel=Form.Panel();
        var accounts=chart.List(owner).Where(a=>a.CashKind!="None" && a.Active).ToList();
        var selected=Form.Records("Cash / bank / processor account",accounts.Select(a=>(a.Id,$"{a.Name} • {(a.OpeningConfirmed?"Confirmed":"Opening unknown")}")));
        var date=Form.Input("Opening date (YYYY-MM-DD)");
        var reason=Form.Input("Evidence or reason for a confirmed zero opening");
        var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase");
        panel.Add(Form.Title("Opening balances"),Form.Text("Unconfirmed balances remain unknown. Approve nonzero evidenced openings as Opening movements in Money. Confirm an actual zero cash balance here. Register principal and cost fields alone do not post openings."),
            selected,date,reason,phrase,_status,Form.Action("Confirm zero cash opening",_status,async()=>
            {
                string account=selected.SelectedId(),evidence=reason.Text,password=phrase.Password;
                phrase.Password="";
                var at=Input.Date(date.Text)??throw new ArgumentException("Enter the opening date.");
                await Task.Run(()=>_book.ConfirmZero(owner,account,at,evidence,password));
                Refresh();
                _status.Text="Zero opening confirmed. Reload this section to refresh account labels.";
            }),_list,_detail);
        _list.SelectionChanged+=(_,_)=>{if(_list.SelectedItem is OpeningBalance opening)Show(opening);};
        Refresh();
        return panel;
    }

    private void Refresh()=>_list.ItemsSource=_book.List(owner);

    private void Show(OpeningBalance opening)
    {
        _detail.Children.Clear();
        _detail.Add(Form.Text(opening.ToString()),Form.Text(opening.Reason));
        if(opening.RevokedAt is not null)return;
        if(opening.MovementId is not null){_detail.Add(Form.Text("To correct this posted opening, prepare a linked reversal in Money. The original evidence stays in history."));return;}
        var reason=Form.Input("Reason to revoke this zero opening");
        var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase");
        var status=Form.Text();
        _detail.Add(reason,phrase,status,Form.Action("Revoke zero opening",status,async()=>
        {
            string explanation=reason.Text,password=phrase.Password;
            phrase.Password="";
            await Task.Run(()=>_book.RevokeZero(owner,opening.Id,explanation,password));
            Refresh();
            _detail.Children.Clear();
        }));
    }
}
