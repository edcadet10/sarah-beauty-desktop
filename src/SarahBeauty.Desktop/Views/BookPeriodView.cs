using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class BookPeriodView
{
    private readonly OwnerSession _owner;
    private readonly BookPeriods _book;
    private readonly Dictionary<string,string> _names;
    private readonly TextBox _from=Form.Input("Book period start (YYYY-MM-DD)"),_through=Form.Input("Book period end (YYYY-MM-DD)");
    private readonly TextBox _reason=Form.Input("Reason for closing this book period"),_evidence=Form.Input("Closing review evidence");
    private readonly TextBlock _status=Form.Text(),_closed=Form.Text();
    private readonly StackPanel _detail=Form.Panel();
    private readonly ListView _history=new(){MaxHeight=200};
    private bool _reviewing;
    private bool _refreshing;
    private int _selectionVersion;

    internal BookPeriodView(BusinessStore store,OwnerSession owner)
    {
        _owner=owner;_book=new(store);new ChartOfAccounts(store).Initialize(owner);
        _names=new OwnerAccess(store).Owners().ToDictionary(o=>o.Id,o=>o.Name);
    }

    internal UIElement Build()
    {
        var panel=Form.Panel();var today=DateOnly.FromDateTime(DateTime.Today);var closed=_book.ClosedThrough(_owner);
        _from.Text=(closed.HasValue && closed.Value<DateOnly.MaxValue?closed.Value.AddDays(1):new(today.Year,today.Month,1)).ToString("yyyy-MM-dd");
        _through.Text=today.ToString("yyyy-MM-dd");
        panel.Add(Form.Title("Book periods"),_closed,Form.Text("Review the full financial report set and each relevant cash account's period-end reconciliation. Closing saves those reports and blocks changes dated through the period end."),
            _from,_through,_reason,_evidence,_status,Form.Action("Review book period closing",_status,async()=>
            {
                if(!await ReplaceReview())return;
                ++_selectionVersion;
                var draft=new BookPeriodDraft(Input.Date(_from.Text)??throw new ArgumentException("Enter the period start."),Input.Date(_through.Text)??throw new ArgumentException("Enter the period end."),_reason.Text,_evidence.Text);
                var preview=await Task.Run(()=>_book.Preview(_owner,draft));
                _reviewing=true;_detail.Children.Clear();_detail.Add(BookPeriodApproval.Build(_book,_owner,preview,_names,ShowSaved));
            }),_detail,Form.Title("Retained book-period reports"),_history);
        Refresh();
        _history.SelectionChanged+=async(_,_)=>
        {
            try
            {
                if(_refreshing || _history.SelectedItem is not HistoryItem item)return;
                int selection=++_selectionVersion;
                if(!await ReplaceReview()){_history.SelectedItem=null;return;}
                var saved=await Task.Run(()=>_book.Open(_owner,item.Value.Id));
                if(selection==_selectionVersion)ShowSaved(saved);
            }
            catch(Exception error){_status.Text=error.Message;}
        };
        return panel;
    }

    private void Refresh()
    {
        _closed.Text="Books closed through: "+(_book.ClosedThrough(_owner)?.ToString("yyyy-MM-dd")??"None");
        _refreshing=true;
        try{_history.ItemsSource=_book.History(_owner).OrderByDescending(p=>p.Through).ThenByDescending(p=>p.AtUtc).Select(p=>new HistoryItem(p,
            $"{p.From:yyyy-MM-dd} – {p.Through:yyyy-MM-dd} • {(p.Reopened?"Reopened":"Closed")} • {p.Id[..8]}")).ToArray();}
        finally{_refreshing=false;}
    }

    private void ShowSaved(ClosedBookPeriod saved)
    {
        ++_selectionVersion;_reviewing=false;_detail.Children.Clear();
        _detail.Add(Form.Title(saved.Reopened?"Reopened book period — retained reports":"Closed book period"),
            Form.Text($"Closed by {_names.GetValueOrDefault(saved.ApprovedBy,"Former owner")} at {saved.AtUtc}. Snapshot code: {saved.Id[..8]}"),BookPeriodSnapshotView.Build(saved.Snapshot,_names));
        foreach(var decision in _book.ReopenHistory(_owner,saved.Id))_detail.Add(Form.Text($"Reopened by {_names.GetValueOrDefault(decision.OwnerId,"Former owner")} at {decision.AtUtc}: {decision.Reason}"));
        if(!saved.Reopened)ReopenFields(saved);
        Refresh();
    }

    private void ReopenFields(ClosedBookPeriod saved)
    {
        var reason=Form.Input("Reason to reopen this book period");var phrase=Form.Secret($"Confirm {_owner.OwnerName}'s passphrase to reopen the book period");var status=Form.Text();
        string request=Guid.NewGuid().ToString();
        _detail.Add(Form.Text("Reopen later book periods first. This retains the saved reports. Bank reconciliations stay protected until you explicitly reopen them too."),reason,phrase,status,
            Form.Action("Reopen book period",status,async()=>
            {
                string why=reason.Text,password=phrase.Password;phrase.Password="";
                await Task.Run(()=>_book.Reopen(_owner,saved.Id,why,request,password));
                ShowSaved(await Task.Run(()=>_book.Open(_owner,saved.Id)));
            }));
    }

    private async Task<bool> ReplaceReview()
    {
        if(!_reviewing)return true;
        var dialog=new ContentDialog{XamlRoot=_detail.XamlRoot,Title="Replace this period review?",Content="The captured review and unsaved confirmation will be discarded.",PrimaryButtonText="Replace review",CloseButtonText="Keep review",DefaultButton=ContentDialogButton.Close};
        return await dialog.ShowAsync()==ContentDialogResult.Primary;
    }
    private sealed record HistoryItem(BookPeriodSummary Value,string Label){public override string ToString()=>Label;}
}
