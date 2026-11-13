using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class DocumentDecisionView(BusinessStore store,OwnerSession owner)
{
    private readonly SearchableRecords _versions=new("Document version"),_evidence=new("Document decision supporting file","No supporting file selected");
    private readonly TextBox _date=Form.Input("Decision date (YYYY-MM-DD)"),_reason=Form.Input("Private document decision reason");
    private readonly ComboBox _action=Form.Select("Document decision",[],"");
    private readonly TextBlock _status=Form.Text();
    private readonly ContentControl _controls=new();
    private readonly StackPanel _detail=Form.Panel(),_editor=Form.Panel(),_history=Form.Panel();
    private DocumentVersion[] _documents=[];
    private IReadOnlyList<DocumentDecision> _decisions=[];
    private StoredAttachment[] _files=[];
    private DocumentVersion? _selected;
    private DocumentDecisionDraft? _retryDraft;
    private string _request="";
    private (string,string,string,string) _baseline;
    private bool _initialized;
    private CancellationTokenSource? _cancellation;
    private ContentDialog? _dialog;
    private PasswordBox? _phrase;

    internal UIElement Build()
    {
        var fields=Form.Panel();fields.Add(_versions.Build(),Form.Action("Review selected document",_status,()=>Run(Select)),Form.Action("Refresh document versions and history",_status,()=>Run(Refresh)),_detail,_editor,_history);_controls.Content=fields;
        var panel=Form.Panel();panel.Add(Form.Title("Document decisions"),Form.Text("Review an exact issued quote or unused invoice. Retained decisions preserve the original document and PDF. Accepted agreements and invoices with payment or credit-note history require their linked cancellation or adjustment workflows."),_controls,
            Form.Action("Cancel document operation",_status,()=>{Cancel();return Task.CompletedTask;}),_status);
        panel.Loaded+=async(_,_)=>{if(_initialized)return;try{await Run(Load);}catch(Exception error){_status.Text=error.Message;}};
        panel.Unloaded+=(_,_)=>Leave();return panel;
    }
    internal void Leave()=>Cancel();
    private void Cancel(){if(_phrase is not null)_phrase.Password="";_dialog?.Hide();_cancellation?.Cancel();}
    private async Task Run(Func<CancellationToken,Task> action)
    {
        if(_cancellation is not null)throw new ArgumentException("Wait for the current document operation.");_cancellation=new();_controls.IsEnabled=false;
        try{await action(_cancellation.Token);}
        catch(OperationCanceledException){_status.Text="Document operation cancelled. Refresh retained history before retrying a decision.";}
        finally{if(_phrase is not null)_phrase.Password="";_phrase=null;_dialog=null;_controls.IsEnabled=true;_cancellation.Dispose();_cancellation=null;}
    }
    private async Task Load(CancellationToken cancellation)
    {
        var data=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>
        {
            token.ThrowIfCancellationRequested();var versions=new QuoteBook(store).Versions(owner).Where(v=>v.Snapshot.Kind is DocumentKind.Quote or DocumentKind.Invoice).ToArray();
            var history=new DocumentDecisions(store).History(owner);var files=new ManagedAttachments(store).History(owner).Where(f=>f.Version.Category!="Customer photo").ToArray();token.ThrowIfCancellationRequested();return(versions,history,files);
        },token),cancellation);
        cancellation.ThrowIfCancellationRequested();_documents=data.versions;_decisions=data.history;_files=data.files;_initialized=true;
        _versions.Update(_documents.Select(v=>(v.Id,v+" • "+v.Snapshot.Client.Name+" • "+v.Id[..8])),_selected?.Id);
        if(_selected is not null)Show(_documents.Single(v=>v.Id==_selected.Id));_status.Text="Saved document versions and retained decisions loaded.";
    }
    private async Task Refresh(CancellationToken cancellation){if(await Discard(cancellation))await Load(cancellation);}
    private async Task Select(CancellationToken cancellation)
    {
        var version=_documents.SingleOrDefault(v=>v.Id==_versions.SelectedId)??throw new ArgumentException("Choose a saved document version.");
        if(!await Discard(cancellation))return;Show(version);_status.Text="Review the exact document version before recording a decision.";
    }
    private (string,string,string,string) Fields()=>(_date.Text,_reason.Text,_action.SelectedItem?.ToString()??"",_evidence.SelectedId);
    private async Task<bool> Discard(CancellationToken cancellation)
    {
        if(_selected is null||Fields()==_baseline)return true;
        _dialog=new(){XamlRoot=_controls.XamlRoot,Title="Discard this document decision draft?",Content="The unsaved date, action, reason and supporting-file selection will be replaced.",PrimaryButtonText="Discard draft",CloseButtonText="Keep editing",DefaultButton=ContentDialogButton.Close};
        bool discard=await _dialog.ShowAsync()==ContentDialogResult.Primary;cancellation.ThrowIfCancellationRequested();return discard;
    }
}
