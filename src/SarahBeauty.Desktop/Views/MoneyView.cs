using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class MoneyView
{
    private readonly BusinessStore _store;
    private readonly OwnerSession _owner;
    private readonly FinanceBook _book;
    private readonly List<Account> _accounts;
    private readonly ListView _list = new(){MaxHeight=220};
    private readonly StackPanel _detail=Form.Panel();
    private readonly TextBlock _status=Form.Text();

    internal MoneyView(BusinessStore store,OwnerSession owner)
    {
        (_store,_owner,_book)=(store,owner,new(store));
        var chart=new ChartOfAccounts(store);
        chart.Initialize(owner);
        _accounts=chart.List(owner);
    }

    internal UIElement Build()
    {
        var panel=Form.Panel();
        var kinds=Form.Select("Movement",Enum.GetNames<FinancialKind>().Where(k=>k!=nameof(FinancialKind.AssetDisposal)).ToArray(),"Expense");
        panel.Add(Form.Title("Money and approvals"),Form.Text("Drafts do not change the books. Review and approve a saved draft to post it."),
            kinds,Form.Action("Prepare movement",_status,()=>
            {
                Edit(new FinancialDraft{Kind=Enum.Parse<FinancialKind>((string)kinds.SelectedItem),
                    Date=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now,"Eastern Standard Time").DateTime)});
                return Task.CompletedTask;
            }),_status,_list,_detail);
        _list.ItemsSource=_book.Drafts(_owner).OrderByDescending(d=>d.Status=="Draft").ToList();
        _list.SelectionChanged+=(_,_)=>{if(_list.SelectedItem is FinancialDraft draft)Review(draft);};
        return panel;
    }

    private void Edit(FinancialDraft draft)
    {
        _detail.Children.Clear();
        var fields=new FinancialFields(_store,_owner,_accounts,draft);
        var status=Form.Text();
        _detail.Add(Form.Title($"{draft.Kind} draft"),fields.Build(),status,Form.Action("Save draft for review",status,async()=>
        {
            var value=fields.Read(draft);
            var saved=await Task.Run(()=>_book.SaveDraft(_owner,value));
            _list.ItemsSource=_book.Drafts(_owner);
            Review(saved);
        }));
    }

    private void Review(FinancialDraft draft)
    {
        _detail.Children.Clear();
        var status=Form.Text();
        _detail.Add(Form.Title(draft.Status=="Posted"?"Posted movement":"Review saved draft"),Form.Text(draft.ToString()));
        try
        {
            var lines=draft.Status=="Posted"?_book.Journal(_owner,draft.PostedBatchId):_book.Preview(_owner,draft);
            _detail.Add(Form.Text(string.Join("\n",lines.Select(l=>$"{_accounts.Single(a=>a.Id==l.AccountId).Name} • Debit {Money.Display(l.DebitCents)} • Credit {Money.Display(l.CreditCents)}"))));
        }
        catch(Exception error){status.Text=error.Message;}
        _detail.Add(status);
        if(draft.Status!="Draft")return;
        var phrase=Form.Secret($"Confirm {_owner.OwnerName}'s passphrase to approve");
        _detail.Add(Form.Action("Edit this draft",status,()=>{Edit(draft);return Task.CompletedTask;}),phrase,
            Form.Action("Approve saved draft",status,async()=>
            {
                string password=phrase.Password;
                phrase.Password="";
                await Task.Run(()=>_book.Approve(_owner,draft.Id,draft.Revision,password));
                _list.ItemsSource=_book.Drafts(_owner);
                _detail.Children.Clear();
                _detail.Add(Form.Text("Posted once. The approver and time are recorded in the activity history."));
            }));
    }
}
