using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class CancellationView(BusinessStore store,OwnerSession owner)
{
    private readonly CancellationCases _book=new(store);
    private readonly StackPanel _detail=Form.Panel();
    private readonly ListView _drafts=new(){MaxHeight=200};
    private readonly ListView _history=new(){MaxHeight=220};
    private readonly TextBlock _status=Form.Text();
    private CancellationFields? _fields;
    private CancellationDraft? _editing;
    private int _version;
    private bool _refreshing;

    internal UIElement Build()
    {
        new ChartOfAccounts(store).Initialize(owner);var panel=Form.Panel();var booking=new SearchableRecords("Booking for cancellation case");
        var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now,"America/New_York").DateTime);
        var date=Form.Input("New cancellation decision date (YYYY-MM-DD)",today.ToString("yyyy-MM-dd"));
        booking.Update(new BookingBook(store).List(owner).Select(b=>(b.Id,b.ToString()+" • "+b.Id[..8])));
        panel.Add(Form.Title("Cancellation cases"),Form.Text("Review the actual agreement and each payment outcome. A draft or preview changes no booked work, cash, or invoice balance."),booking.Build(),date,
            Form.Action("Prepare cancellation case",_status,async()=>
            {
                if(!await Replace())return;
                string id=booking.SelectedId;if(id.Length==0)throw new ArgumentException("Choose the booking for this cancellation case.");
                var current=new BookingBook(store).List(owner).Single(b=>b.Id==id);
                var at=SarahBeauty.Business.Rules.Input.Date(date.Text)??throw new ArgumentException("Enter the new cancellation decision date.");
                await Edit(new(){BookingId=id,RequestedDate=at,Date=at,TermsVersionId=current.AcceptedQuoteVersionId});
            }),_status,Form.Text("Saved cancellation drafts"),_drafts,Form.Text("Approved cancellation history"),_history,_detail);
        _drafts.SelectionChanged+=async(_,_)=>
        {
            try
            {
                if(_refreshing || _drafts.SelectedItem is not DraftItem item)return;
                if(!await Replace()){_refreshing=true;_drafts.SelectedItem=null;_refreshing=false;return;}
                await Review(item.Value);
            }
            catch(Exception error){_status.Text=error.Message;}
        };
        _history.SelectionChanged+=async(_,_)=>
        {
            try
            {
                if(_refreshing || _history.SelectedItem is not HistoryItem item)return;
                if(!await Replace()){_refreshing=true;_history.SelectedItem=null;_refreshing=false;return;}
                int version=++_version;
                var saved=await Task.Run(()=>_book.Open(owner,item.Value.Id));
                if(version==_version)ShowSaved(saved);
            }
            catch(Exception error){_status.Text=error.Message;}
        };
        Refresh();return panel;
    }

    private void Refresh()
    {
        _refreshing=true;
        try
        {
            _drafts.ItemsSource=_book.Drafts(owner).Where(d=>d.Status=="Draft").Select(d=>new DraftItem(d)).ToArray();
            _history.ItemsSource=_book.History(owner).OrderByDescending(c=>c.AtUtc).Select(c=>new HistoryItem(c)).ToArray();
        }
        finally{_refreshing=false;}
    }

    private async Task Edit(CancellationDraft draft)
    {
        int version=++_version;
        var sources=await Task.Run(()=>new CancellationSources(store).Load(owner,draft.BookingId,draft.Date??DateOnly.FromDateTime(DateTime.Today)));
        if(version!=_version)return;
        var fields=new CancellationFields(draft,sources,new ChartOfAccounts(store).List(owner),new BookingBook(store).List(owner));_fields=fields;_editing=draft;
        var status=Form.Text();_detail.Children.Clear();_detail.Add(Form.Title("Cancellation case draft"),Form.Text(sources.BookingName),fields.Build(),status,
            Form.Action("Save cancellation draft for review",status,async()=>
            {
                var value=fields.Read(draft);var saved=await Task.Run(()=>_book.SaveDraft(owner,value));Refresh();
                if(version==_version)await Review(saved);
            }),new Expander{Header="Captured cancellation source balances",Content=CancellationReviewView.Sources(sources),HorizontalAlignment=HorizontalAlignment.Stretch});
    }

    private async Task Review(CancellationDraft draft)
    {
        int version=++_version;_fields=null;_editing=null;_detail.Children.Clear();var status=Form.Text();
        _detail.Add(Form.Title("Review saved cancellation draft"),Form.Text(draft.Name),status,
            Form.Action("Edit cancellation draft",status,()=>Edit(draft)));
        try
        {
            var preview=await Task.Run(()=>_book.Preview(owner,draft));
            if(version!=_version)return;
            _detail.Add(Form.Title("Proposed combined cancellation"),CancellationReviewView.Build(preview,new ChartOfAccounts(store).List(owner)),
                Form.Text("Preview only. This review has not cancelled the booking or posted these actions."),
                CancellationApprovalView.Build(_book,owner,preview,saved=>
                {
                    Refresh();if(version==_version)ShowSaved(saved);
                    else _status.Text="Cancellation approved. Open its retained record in Approved cancellation history.";
                }));
        }
        catch(Exception error){if(version==_version)status.Text=error.Message;}
    }

    private void ShowSaved(ApprovedCancellation saved)
    {
        ++_version;_fields=null;_editing=null;_detail.Children.Clear();
        string actor=new OwnerAccess(store).Owners().FirstOrDefault(o=>o.Id==saved.ApprovedBy).Name??saved.ApprovedBy;
        _detail.Add(Form.Title("Approved cancellation decision"),Form.Text($"Approved by {actor} at {saved.AtUtc}\nDecision code: {saved.Id}\nThis retained record keeps its original agreement and source balances."),
            CancellationReviewView.Build(saved.Snapshot,new ChartOfAccounts(store).List(owner)));
        foreach(var action in saved.Snapshot.Actions.Where(a=>a.Kind=="Credit note"))
            _detail.Add(new Expander{Header="Cancellation credit note "+saved.Snapshot.After.Documents.Single(d=>d.Id==action.ResultId).Snapshot.Number,
                Content=PdfArtifactView.Build(store,owner,action.ResultId),HorizontalAlignment=HorizontalAlignment.Stretch});
    }

    private async Task<bool> Replace()
    {
        if(_fields is null || _editing is null || !_fields.Changed(_editing))return true;
        var dialog=new ContentDialog{XamlRoot=_detail.XamlRoot,Title="Discard unsaved cancellation fields?",Content="The current case fields have not been saved.",
            PrimaryButtonText="Discard unsaved fields",CloseButtonText="Keep editing",DefaultButton=ContentDialogButton.Close};
        return await dialog.ShowAsync()==ContentDialogResult.Primary;
    }
    private sealed record DraftItem(CancellationDraft Value){public override string ToString()=>$"{Value.Date:yyyy-MM-dd} • {Value.Name} • {Value.Id[..8]}";}
    private sealed record HistoryItem(CancellationSummary Value){public override string ToString()=>$"{Value.DecisionDate:yyyy-MM-dd} • {Value.Reason} • {Value.Id[..8]}";}
}
