using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Setup;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class BusinessProfileView(BusinessStore store,OwnerSession owner,Action<string>? displayNameChanged=null)
{
    private readonly BusinessProfileFields _fields=new();
    private readonly ContentControl _controls=new(),_history=new();
    private readonly TextBox _reason=Form.Input("Private reason for this business-profile review");
    private readonly TextBlock _status=Form.Text(),_revision=Form.Text();
    private IReadOnlyList<BusinessProfileVersion> _versions=[];
    private long _draftRevision;
    private bool _initialized;
    private BusinessProfileChange? _retryDraft;
    private string _request="";
    private CancellationTokenSource? _cancellation;
    private ContentDialog? _dialog;
    private PasswordBox? _phrase;
    private Task? _running;

    internal UIElement Build()
    {
        _reason.AcceptsReturn=true;_reason.MinHeight=70;var fields=Form.Panel();fields.Add(_revision,_fields.Build(),_reason,Form.Action("Review and confirm business profile",_status,()=>Run(Confirm)),
            Form.Action("Refresh saved business-profile history",_status,()=>Run(c=>Load(c,false))),Form.Action("Reload current business profile and discard draft",_status,()=>Run(Reload)),_history);_controls.Content=fields;_controls.IsEnabled=false;
        var panel=Form.Panel();panel.Add(Form.Title("Business setup"),Form.Text("Keep unknown setup facts unconfirmed. These private profile facts and review reasons stay out of readable Sheets exports; full encrypted archives retain them. Each review records your assessment and does not verify an EIN, legal formation, banking, or tax registration."),
            Form.Text("Saving this profile does not activate a LIVE business, import earlier-business records, confirm ownership percentages, post an opening balance, or change an issued document."),_controls,
            Form.Action("Cancel business-profile operation",_status,()=>{Cancel();return Task.CompletedTask;}),_status);
        panel.Loaded+=async(_,_)=>{if(_initialized)return;try{if(_running is{IsCompleted:false} previous)await previous;if(panel.IsLoaded&&!_initialized)await Run(c=>Load(c,true));}catch(Exception error){_status.Text=error.Message;}};panel.Unloaded+=(_,_)=>Leave();return panel;
    }
    internal void Leave()=>Cancel();
    private void Cancel(){if(_phrase is not null)_phrase.Password="";_dialog?.Hide();_cancellation?.Cancel();}
    private Task Run(Func<CancellationToken,Task> action)
    {
        if(_cancellation is not null)throw new ArgumentException("Wait for the current business-profile operation.");_running=RunCore(action);return _running;
    }
    private async Task RunCore(Func<CancellationToken,Task> action)
    {
        _cancellation=new();_controls.IsEnabled=false;
        try{await action(_cancellation.Token);}
        catch(OperationCanceledException){_status.Text="Business-profile operation cancelled. Refresh saved history before retrying a confirmation.";}
        finally{if(_phrase is not null)_phrase.Password="";_phrase=null;_dialog=null;_controls.IsEnabled=_initialized;_cancellation.Dispose();_cancellation=null;}
    }
    private async Task Load(CancellationToken cancellation,bool reset)
    {
        var data=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>
        {
            token.ThrowIfCancellationRequested();var versions=new BusinessProfiles(store).History(owner,token);var files=new ManagedAttachments(store).History(owner).Where(f=>f.Version.Category!="Customer photo").ToArray();string name=store.BusinessName(owner);token.ThrowIfCancellationRequested();return(versions,files,name);
        },token),cancellation);
        cancellation.ThrowIfCancellationRequested();_versions=data.versions;
        if(reset){_draftRevision=_versions.LastOrDefault()?.Revision??0;_fields.Set(_versions.LastOrDefault()?.Profile??new(){DisplayName=data.name},data.files);_reason.Text="";_retryDraft=null;_request="";}else _fields.UpdateFiles(data.files);
        _initialized=true;_revision.Text="Reviewing profile revision "+_draftRevision+" • Latest saved revision "+(_versions.LastOrDefault()?.Revision??0);ShowHistory();displayNameChanged?.Invoke(data.name);_status.Text=reset?"Current business profile loaded. Unknown setup facts remain unconfirmed.":"Saved history refreshed. Your profile fields and reviewed revision are preserved.";
    }
    private async Task Reload(CancellationToken cancellation)
    {
        _dialog=new(){XamlRoot=_controls.XamlRoot,Title="Discard the business-profile draft?",Content="Reloading replaces the unsaved profile fields, review states, file selections and private change reason.",PrimaryButtonText="Discard and reload",CloseButtonText="Keep editing",DefaultButton=ContentDialogButton.Close};
        if(await _dialog.ShowAsync()!=ContentDialogResult.Primary){_status.Text="Profile draft preserved.";return;}cancellation.ThrowIfCancellationRequested();await Load(cancellation,true);
    }
}
