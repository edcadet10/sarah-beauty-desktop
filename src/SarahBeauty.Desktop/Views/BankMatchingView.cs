using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class BankMatchingView
{
    private readonly OwnerSession _owner;
    private readonly BusinessStore _store;
    private readonly BankMatching _book;
    private readonly ComboBox _account;
    private readonly TextBox _date=Form.Input("Match effective date (YYYY-MM-DD)"),_reason=Form.Input("Reason for the matching decision");
    private readonly StackPanel _sources=Form.Panel(),_review=Form.Panel();
    private readonly TextBlock _status=Form.Text();
    private readonly List<MatchPortion> _portions=[];
    private readonly ListView _pending=new(){MaxHeight=160};
    private BankMatchSources? _fields;
    private string _loadedAccount="";
    private DateOnly? _loadedDate;

    internal BankMatchingView(BusinessStore store,OwnerSession owner)
    {
        (_store,_owner,_book)=(store,owner,new(store));
        var chart=new ChartOfAccounts(store);chart.Initialize(owner);
        _account=Form.Records("Matching account",chart.List(owner).Where(a=>a.CashKind!="None").Select(a=>(a.Id,a.ToString())));
    }

    internal UIElement Build()
    {
        var panel=Form.Panel();
        panel.Add(Form.Title("Bank matching"),Form.Text("Match actual bank transactions to posted cash entries on the same account. Prepare several portions for a combined match. Matching and unmatching retain their history and create no new money."),
            _account,_date,_reason,_status,Form.Action("Load matching source balances",_status,async()=>
            {
                if(_portions.Count>0 && !await DiscardPortions())return;
                await Load();
            }),_sources,_pending,Form.Action("Remove selected prepared portion",_status,()=>
            {
                if(_pending.SelectedIndex>=0){_portions.RemoveAt(_pending.SelectedIndex);ShowPending();_review.Children.Clear();}
                return Task.CompletedTask;
            }),_review);
        return panel;
    }

    private async Task Load()
    {
        string account=_account.SelectedId();
        var date=Input.Date(_date.Text)??throw new ArgumentException("Enter the effective matching date.");
        if(account.Length==0)throw new ArgumentException("Choose the matching account.");
        var data=await Task.Run(()=>_book.LoadSources(_owner,account,date));
        (_loadedAccount,_loadedDate)=(account,date);
        _portions.Clear();_review.Children.Clear();ShowPending();_sources.Children.Clear();
        _fields=new(data.BankLines,data.CashLines,data.BankPositions,data.CashPositions);
        var undo=new BankUnmatchFields(data.History,data.BankLines,data.CashLines,date);
        _sources.Add(Form.Title("Prepare matching portions"),_fields.Build(),
            Form.Action("Add matching portion",_status,()=>{AddPortion();return Task.CompletedTask;}),
            Form.Action("Review prepared matching",_status,async()=>await Review(_portions.ToArray())),
            Form.Title("Undo an existing match"),undo.Build(),Form.Action("Review unmatch",_status,async()=>
            {
                if(_portions.Count>0)throw new ArgumentException("Remove prepared new portions before reviewing an unmatch.");
                await Review([undo.Read()]);
            }),BankMatchHistory.Build(data,date,new OwnerAccess(_store).Owners().ToDictionary(o=>o.Id,o=>o.Name)));
    }

    private void AddPortion()
    {
        CheckLoaded();
        var portion=_fields!.Read();
        var proposed=_portions.ToList();
        int index=proposed.FindIndex(p=>p.BankLineId==portion.BankLineId && p.JournalLineId==portion.JournalLineId);
        if(index>=0)proposed[index]=portion with{Cents=checked(proposed[index].Cents+portion.Cents)};
        else proposed.Add(portion);
        _book.Preview(_owner,new(_loadedDate!.Value,_reason.Text,proposed.ToArray()));
        _portions.Clear();_portions.AddRange(proposed);ShowPending();_review.Children.Clear();
    }

    private async Task Review(MatchPortion[] portions)
    {
        CheckLoaded();
        var draft=new BankMatchDraft(_loadedDate!.Value,_reason.Text,portions);
        var preview=await Task.Run(()=>_book.Preview(_owner,draft));
        _review.Children.Clear();
        _review.Add(BankMatchApproval.Build(_book,_owner,preview,_account.SelectedItem?.ToString()??"",portions.Select(p=>(p.ReversesId.Length>0?"Unmatch: ":"")+_fields!.Describe(p)),async()=>
        {
            await Load();_status.Text="Matching approval saved.";
        }));
    }

    private void CheckLoaded()
    {
        if(_fields is null || _account.SelectedId()!=_loadedAccount || Input.Date(_date.Text)!=_loadedDate)
            throw new ArgumentException("Load source balances again after changing the account or effective date.");
    }

    private void ShowPending()=>_pending.ItemsSource=_portions.Select(p=>_fields!.Describe(p)).ToArray();

    private async Task<bool> DiscardPortions()
    {
        var dialog=new ContentDialog{XamlRoot=_sources.XamlRoot,Title="Reload matching sources?",Content="The prepared matching portions will be discarded.",
            PrimaryButtonText="Reload",CloseButtonText="Keep editing",DefaultButton=ContentDialogButton.Close};
        return await dialog.ShowAsync()==ContentDialogResult.Primary;
    }
}
