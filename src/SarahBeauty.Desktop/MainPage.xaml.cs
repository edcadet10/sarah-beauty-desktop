using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Storage;
using SarahBeauty.Business.Setup;
using SarahBeauty_Desktop.Storage;
using SarahBeauty_Desktop.Views;

namespace SarahBeauty_Desktop;

public sealed partial class MainPage : Page
{
    private BusinessStore? _store;
    private OwnerAccess? _access;
    private OwnerSession? _session;
    private BusinessMode _mode=BusinessMode.Test;
    private AutomaticBackupRuntime? _backupRuntime;
    private GoogleArchiveRuntime? _googleRuntime;
    private GoogleSheetsRuntime? _sheetsRuntime;
    private readonly List<Task> _stoppingBackups=[];

    public MainPage()
    {
        InitializeComponent();
        InitializeOwnerActivity();
        if(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SARAHBEAUTY_TEST_DIRECTORY")))OpenCurrentStore();else ShowWorkspaceChoices();
    }
    private void OpenCurrentStore()
    {
        StopBackups();_session?.Lock();_session=null;_store=null;_access=null;
        try
        {
            if(_mode==BusinessMode.Live&&!new LiveBusinesses(AppDataPaths.GetLiveFolder()).Exists){ShowMissingLive();return;}
            _store = _mode==BusinessMode.Test?new BusinessStore(new ActiveTestStores(AppDataPaths.EnsureTestFolderExists()).ResolveDatabase()):new LiveBusinesses(AppDataPaths.GetLiveFolder()).Open();
            _access = new OwnerAccess(_store);
            StartBackups();
            ShowSignIn();
        }
        catch (Exception error)
        {
            var panel=Form.Panel();var status=Form.Text();panel.Add(Form.Title(BusinessModes.StoredName(_mode)+" storage could not open"),Form.Text(error.Message),WorkspaceChoiceAction(status),RecoveryActions(),status);ShowPanel(panel);
        }
    }

    private void ShowSignIn(string? reason=null)
    {
        HideOwnerContent();
        var panel=Form.Panel();var access=_access!;int generation=_workspaceGeneration;var status=Form.Text();
        if(reason is not null)panel.Add(Form.Text(reason));
        panel.Add(Form.Text(BusinessModes.StoredName(_mode)+" • Local business records"), OwnerView.Build(access, session =>
        {
            bool retained=false;
            try
            {
                if(generation!=_workspaceGeneration||!ReferenceEquals(_access,access)||_stoppingOwnerActivity||!access.IsSessionCurrent(session))return;
                _session=session;_ownerActivity=new(access,session);ShowWorkspace();retained=true;
            }
            finally{if(!retained){session.Lock();if(ReferenceEquals(_session,session)){_session=null;_ownerActivity=null;}}}
        }),WorkspaceChoiceAction(status),RecoveryActions(),status);ShowPanel(panel);
    }

    private void ShowWorkspace()
    {
        var owner=_session!;
        Host.Content=new WorkspaceView(_store!,owner,LockOwner,ShowRetired,()=>_backupRuntime?.Status??"Stopped",()=>_googleRuntime?.Status??"Stopped",()=>_sheetsRuntime?.Status??"Stopped",()=>{if(ReferenceEquals(_session,owner))ShowSignIn("Passphrase changed. Sign in with your new passphrase.");}).Build();
    }
    private void ShowArchiveReview()
    {
        HideOwnerContent();var panel=Form.Panel();var status=Form.Text();
        panel.Add(Form.Action("Back to owner sign-in",status,()=>{OpenCurrentStore();return Task.CompletedTask;}),status,new RestoreReviewView(mode:_mode).Build());ShowPanel(panel);
    }
    private StackPanel RecoveryActions()
    {
        var panel=Form.Panel();var status=Form.Text();panel.Add(Form.Action("Review an encrypted recovery archive",status,()=>{ShowArchiveReview();return Task.CompletedTask;}),
            Form.Action("Activate a prepared recovered copy",status,()=>{ShowActivation();return Task.CompletedTask;}),status);return panel;
    }
    private void ShowActivation()
    {
        HideOwnerContent();var panel=Form.Panel();var status=Form.Text();panel.Add(Form.Action("Back to owner sign-in",status,()=>{OpenCurrentStore();return Task.CompletedTask;}),status,
            new ActivateStoreView(AppDataPaths.EnsureFolderExists(_mode),result=>EnterAuthenticatedStore(result.Store,result.Owner),_mode).Build());ShowPanel(panel);
    }
    private void ShowRetired()
    {
        StopBackups();_session?.Lock();_session=null;_store=null;_access=null;var panel=Form.Panel();panel.Add(Form.Title("This local instance is retired"),Form.Text("Saved records and the verified archive are preserved. Activate a prepared recovered copy to continue."),RecoveryActions());ShowPanel(panel);
    }
    private void StopBackups(){_stoppingBackups.RemoveAll(task=>task.IsCompleted);if(_backupRuntime is not null)_stoppingBackups.Add(_backupRuntime.StopAsync());if(_googleRuntime is not null)_stoppingBackups.Add(_googleRuntime.StopAsync());if(_sheetsRuntime is not null)_stoppingBackups.Add(_sheetsRuntime.StopAsync());_backupRuntime=null;_googleRuntime=null;_sheetsRuntime=null;}
    internal async Task StopBackupsAsync(){_stoppingOwnerActivity=true;_ownerTimer.Stop();HideOwnerContent();StopBackups();await Task.WhenAll(_stoppingBackups);}
    private void ShowPanel(StackPanel panel)=>Host.Content=new ScrollViewer{Padding=new Microsoft.UI.Xaml.Thickness(24),Content=panel};
}
