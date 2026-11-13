using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class ClientDraftDiscardView(BusinessStore store,OwnerSession owner)
{
    private readonly SearchableRecords _drafts=new("Unissued client draft");
    private readonly TextBox _reason=Form.Input("Private reason for discarding this client draft");
    private readonly ContentControl _controls=new();
    private readonly StackPanel _history=Form.Panel();
    private readonly TextBlock _status=Form.Text();
    private ClientDraftDiscardChoice[] _choices=[];
    private ClientDraftDiscardRequest? _retryDraft;
    private string _request="";
    private bool _initialized;
    private CancellationTokenSource? _cancellation;
    private Task? _running;
    private ContentDialog? _dialog;
    private PasswordBox? _phrase;

    internal UIElement Build()
    {
        _reason.AcceptsReturn=true;_reason.MinHeight=70;var fields=Form.Panel();fields.Add(_drafts.Build(),_reason,Form.Action("Review and discard selected client draft",_status,()=>Run(Confirm)),Form.Action("Refresh saved drafts and discard history",_status,()=>Run(Load)),_history);_controls.Content=fields;_controls.IsEnabled=false;
        var panel=Form.Panel();panel.Add(Form.Title("Discard client drafts"),Form.Text("Choose a saved, unissued quote, invoice, receipt, or credit-note draft. Discarding retains its original contents and private reason permanently. Issued documents, payments, journal entries and booking agreements keep their existing history. Private reasons and retained draft snapshots stay out of readable Sheets exports."),_controls,Form.Action("Cancel draft-discard operation",_status,()=>{Leave();return Task.CompletedTask;}),_status);
        panel.Loaded+=async(_,_)=>{if(_initialized)return;try{if(_running is{IsCompleted:false} previous)await previous;if(panel.IsLoaded&&!_initialized)await Run(Load);}catch(Exception error){_status.Text=error.Message;}};panel.Unloaded+=(_,_)=>Leave();return panel;
    }
    internal void Leave(){if(_phrase is not null)_phrase.Password="";_dialog?.Hide();_cancellation?.Cancel();}
    private Task Run(Func<CancellationToken,Task> action)
    {
        if(_cancellation is not null)throw new ArgumentException("Wait for the current draft-discard operation.");_running=RunCore(action);return _running;
    }
    private async Task RunCore(Func<CancellationToken,Task> action)
    {
        _cancellation=new();_controls.IsEnabled=false;
        try{await action(_cancellation.Token);}
        catch(OperationCanceledException){_status.Text="Draft-discard operation cancelled. Refresh retained history before retrying.";}
        finally{if(_phrase is not null)_phrase.Password="";_phrase=null;_dialog=null;_controls.IsEnabled=_initialized;_cancellation.Dispose();_cancellation=null;}
    }
    private async Task Load(CancellationToken cancellation)
    {
        var data=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>
        {
            var history=new ClientDraftDiscards(store).History(owner,token);var choices=Choices();token.ThrowIfCancellationRequested();return(history,choices);
        },token),cancellation);
        cancellation.ThrowIfCancellationRequested();_choices=data.choices;_drafts.Update(_choices.Select(v=>(v.Draft.Id,v.Label)),_drafts.SelectedId);ShowHistory(data.history);_initialized=true;_status.Text="Saved unissued drafts and retained discard history loaded. Your private reason is preserved.";
    }
    private ClientDraftDiscardChoice[] Choices()=>new QuoteBook(store).Drafts(owner).Select(v=>new ClientDraftDiscardChoice(DocumentKind.Quote,v))
        .Concat(new InvoiceBook(store).Drafts(owner).Select(v=>new ClientDraftDiscardChoice(DocumentKind.Invoice,v)))
        .Concat(new ReceiptBook(store).Drafts(owner).Select(v=>new ClientDraftDiscardChoice(DocumentKind.Receipt,v)))
        .Concat(new CreditNoteBook(store).Drafts(owner).Select(v=>new ClientDraftDiscardChoice(DocumentKind.CreditNote,v))).Where(v=>ClientDraftDiscardChoice.Available(v.Draft)).OrderBy(v=>v.Label,StringComparer.OrdinalIgnoreCase).ToArray();
    private void ShowHistory(IReadOnlyList<ClientDraftDiscard> values)
    {
        _history.Children.Clear();_history.Add(Form.Title("Retained client draft discards"));if(values.Count==0)_history.Add(Form.Text("No client drafts have been discarded."));
        foreach(var value in values)_history.Add(Form.Text($"{ClientDraftDiscardChoice.Retained(value).Summary}\nDiscard recorded at {value.RecordedAtUtc}\nPrivate reason: {value.Reason}"));
    }
}
