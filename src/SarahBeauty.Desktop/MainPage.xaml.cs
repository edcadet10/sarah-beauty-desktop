using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;
using SarahBeauty_Desktop.Views;

namespace SarahBeauty_Desktop;

public sealed partial class MainPage : Page
{
    private BusinessStore? _store;
    private OwnerAccess? _access;
    private OwnerSession? _session;
    private AutomaticBackupRuntime? _backupRuntime;
    private GoogleArchiveRuntime? _googleRuntime;
    private GoogleSheetsRuntime? _sheetsRuntime;
    private readonly List<Task> _stoppingBackups=[];

    public MainPage()
    {
        InitializeComponent();
        InitializeOwnerActivity();
        OpenCurrentStore();
    }
    private void OpenCurrentStore()
    {
        StopBackups();_session?.Lock();_session=null;_store=null;_access=null;
        try
        {
            _store = new BusinessStore(new ActiveTestStores(AppDataPaths.EnsureTestFolderExists()).ResolveDatabase());
            _access = new OwnerAccess(_store);
            _backupRuntime=new(_store);_googleRuntime=new(_store);_sheetsRuntime=new(_store);
            ShowSignIn();
        }
        catch (Exception error)
        {
            var panel=Form.Panel();panel.Add(Form.Title("Storage could not open"),Form.Text(error.Message),RecoveryActions());ShowPanel(panel);
        }
    }

    private void ShowSignIn(string? reason=null)
    {
        HideOwnerContent();
        var panel=Form.Panel();
        if(reason is not null)panel.Add(Form.Text(reason));
        panel.Add(Form.Text("TEST • Local business records"), OwnerView.Build(_access!, session =>
        {
            _session = session;
            _ownerActivity=new(_access!,session);
            ShowWorkspace();
        }),RecoveryActions());ShowPanel(panel);
    }

    private void ShowWorkspace()
    {
        Host.Content=new WorkspaceView(_store!,_session!,LockOwner,ShowRetired,()=>_backupRuntime?.Status??"Stopped",()=>_googleRuntime?.Status??"Stopped",()=>_sheetsRuntime?.Status??"Stopped").Build();
    }
    private void ShowArchiveReview()
    {
        _session?.Lock();_session=null;var panel=Form.Panel();var status=Form.Text();
        panel.Add(Form.Action("Back to owner sign-in",status,()=>{OpenCurrentStore();return Task.CompletedTask;}),status,new RestoreReviewView().Build());ShowPanel(panel);
    }
    private StackPanel RecoveryActions()
    {
        var panel=Form.Panel();var status=Form.Text();panel.Add(Form.Action("Review an encrypted recovery archive",status,()=>{ShowArchiveReview();return Task.CompletedTask;}),
            Form.Action("Activate a prepared recovered copy",status,()=>{ShowActivation();return Task.CompletedTask;}),status);return panel;
    }
    private void ShowActivation()
    {
        _session?.Lock();_session=null;var panel=Form.Panel();var status=Form.Text();panel.Add(Form.Action("Back to owner sign-in",status,()=>{OpenCurrentStore();return Task.CompletedTask;}),status,
            new ActivateStoreView(AppDataPaths.EnsureTestFolderExists(),result=>{StopBackups();_session?.Lock();_store=result.Store;_access=new OwnerAccess(_store);_session=result.Owner;_ownerActivity=new(_access,_session);_backupRuntime=new(_store);_googleRuntime=new(_store);_sheetsRuntime=new(_store);ShowWorkspace();}).Build());ShowPanel(panel);
    }
    private void ShowRetired()
    {
        StopBackups();_session?.Lock();_session=null;_store=null;_access=null;var panel=Form.Panel();panel.Add(Form.Title("This local instance is retired"),Form.Text("Saved records and the verified archive are preserved. Activate a prepared recovered copy to continue."),RecoveryActions());ShowPanel(panel);
    }
    private void StopBackups(){_stoppingBackups.RemoveAll(task=>task.IsCompleted);if(_backupRuntime is not null)_stoppingBackups.Add(_backupRuntime.StopAsync());if(_googleRuntime is not null)_stoppingBackups.Add(_googleRuntime.StopAsync());if(_sheetsRuntime is not null)_stoppingBackups.Add(_sheetsRuntime.StopAsync());_backupRuntime=null;_googleRuntime=null;_sheetsRuntime=null;}
    internal async Task StopBackupsAsync(){_stoppingOwnerActivity=true;_ownerTimer.Stop();HideOwnerContent();StopBackups();await Task.WhenAll(_stoppingBackups);}
    private void ShowPanel(StackPanel panel)=>Host.Content=new ScrollViewer{Padding=new Microsoft.UI.Xaml.Thickness(24),Content=panel};
}
