using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class ReconciliationView
{
    private readonly BusinessStore _store;
    private readonly OwnerSession _owner;
    private readonly Reconciliations _book;
    private readonly StatementBook _statements;
    private readonly ComboBox _statement=new(){Header="Statement to reconcile",MinWidth=200};
    private readonly TextBox _reason=Form.Input("Reason for this reconciliation review");
    private readonly TextBlock _status=Form.Text();
    private readonly StackPanel _detail=Form.Panel();
    private readonly ListView _history=new(){MaxHeight=220};
    private readonly Dictionary<string,string> _accounts;
    private bool _editing;

    internal ReconciliationView(BusinessStore store,OwnerSession owner)
    {
        (_store,_owner,_book,_statements)=(store,owner,new(store),new(store));
        var chart=new ChartOfAccounts(store);chart.Initialize(owner);
        _accounts=chart.List(owner).ToDictionary(a=>a.Id,a=>a.Name);
    }

    internal UIElement Build()
    {
        var panel=Form.Panel();
        panel.Add(Form.Title("Reconciliation"),Form.Text("Review an imported statement in date order. Match actual bank transactions first, then support real outstanding deposits or payments. A zero difference alone does not explain missing items."),
            _statement,_reason,_status,Form.Action("Load reconciliation sources",_status,async()=>
            {
                if(!await ReplaceEditor())return;
                string id=_statement.SelectedId();
                var sources=await Task.Run(()=>_book.Sources(_owner,id));
                Edit(sources);
            }),_detail,Form.Title("Retained reconciliation snapshots"),_history);
        Refresh();
        _history.SelectionChanged+=async(_,_)=>
        {
            try
            {
                if(_history.SelectedItem is not HistoryItem item)return;
                if(!await ReplaceEditor()){_history.SelectedItem=null;return;}
                ShowSaved(await Task.Run(()=>_book.Open(_owner,item.Value.Id)));
            }
            catch(Exception error){_status.Text=error.Message;}
        };
        return panel;
    }

    private void Refresh()
    {
        string selected=_statement.SelectedId();
        var source=Form.Records("",_statements.List(_owner).Where(s=>!s.Withdrawn).OrderByDescending(s=>s.Through).Select(s=>(s.Id,$"{_accounts[s.AccountId]} • {s.From:yyyy-MM-dd} – {s.Through:yyyy-MM-dd} • {s.RowCount} source rows")),selected);
        _statement.ItemsSource=source.ItemsSource;_statement.SelectedItem=source.SelectedItem;
        _history.ItemsSource=_book.History(_owner).OrderByDescending(h=>h.AsOfDate).ThenByDescending(h=>h.AtUtc).Select(h=>new HistoryItem(h,
            $"{_accounts[h.AccountId]} • {h.AsOfDate:yyyy-MM-dd} • {(h.Reopened?"Reopened":"Completed")} • {h.Id[..8]}")).ToArray();
    }

    private void Edit(ReconciliationSources sources)
    {
        _detail.Children.Clear();_editing=true;
        var fields=new ReconciliationFields(sources);var review=Form.Panel();var status=Form.Text();
        _detail.Add(Form.Title(sources.AccountName+" • Through "+sources.Statement.Through.ToString("yyyy-MM-dd")),
            Form.Text($"Opening: {(sources.OpeningConfirmed?"Confirmed":"Unconfirmed")} • Unmatched bank rows: {sources.Bank.Count(b=>b.RemainingCents>0)} • Unmatched cash entries: {sources.Cash.Count(c=>c.RemainingCents>0)}"),
            fields.Build(),status,Form.Action("Preview reconciliation",status,async()=>
            {
                if(_statement.SelectedId()!=sources.Statement.Id)throw new ArgumentException("Load sources again after selecting another statement.");
                var draft=fields.Read(_reason.Text);
                var preview=await Task.Run(()=>_book.Preview(_owner,draft));
                review.Children.Clear();review.Add(ReconciliationApproval.Build(_book,_owner,preview,ShowSaved));
            }),review);
    }

    private void ShowSaved(CompletedReconciliation saved)
    {
        _editing=false;_detail.Children.Clear();
        var names=new OwnerAccess(_store).Owners().ToDictionary(o=>o.Id,o=>o.Name);
        string actor=names.GetValueOrDefault(saved.ApprovedBy,"Former owner");
        _detail.Add(Form.Title(saved.Reopened?"Reopened reconciliation — retained snapshot":"Completed reconciliation"),
            Form.Text($"Approved by {actor} at {saved.AtUtc}. Snapshot code: {saved.Id[..8]}"),ReconciliationSnapshotView.Build(saved.Snapshot,historical:true));
        foreach(var decision in _book.ReopenHistory(_owner,saved.Id))_detail.Add(Form.Text($"Reopened by {names.GetValueOrDefault(decision.OwnerId,"Former owner")} at {decision.AtUtc}: {decision.Reason}"));
        if(!saved.Reopened)ReopenFields(saved);
        Refresh();
    }

    private void ReopenFields(CompletedReconciliation saved)
    {
        var reason=Form.Input("Reason to reopen this reconciliation");
        var phrase=Form.Secret($"Confirm {_owner.OwnerName}'s passphrase to reopen reconciliation");var status=Form.Text();
        string request=Guid.NewGuid().ToString();
        _detail.Add(Form.Text("Reopening retains this snapshot. Reopen later reconciliations first; if the book period is closed, reopen that period first."),reason,phrase,status,
            Form.Action("Reopen reconciliation",status,async()=>
            {
                string text=reason.Text,password=phrase.Password;phrase.Password="";
                await Task.Run(()=>_book.Reopen(_owner,saved.Id,text,request,password));
                ShowSaved(await Task.Run(()=>_book.Open(_owner,saved.Id)));
            }));
    }

    private async Task<bool> ReplaceEditor()
    {
        if(!_editing)return true;
        var dialog=new ContentDialog{XamlRoot=_detail.XamlRoot,Title="Replace the current reconciliation review?",Content="Unsaved outstanding-item and correction selections will be discarded.",
            PrimaryButtonText="Replace review",CloseButtonText="Keep editing",DefaultButton=ContentDialogButton.Close};
        return await dialog.ShowAsync()==ContentDialogResult.Primary;
    }

    private sealed record HistoryItem(ReconciliationSummary Value,string Label){public override string ToString()=>Label;}
}
