using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;
using SarahBeauty_Desktop.Views;

namespace SarahBeauty_Desktop;

public sealed partial class MainPage : Page
{
    private BusinessStore? _store;
    private OwnerAccess? _access;
    private OwnerSession? _session;

    public MainPage()
    {
        InitializeComponent();
        try
        {
            _store = new BusinessStore(System.IO.Path.Combine(AppDataPaths.EnsureTestFolderExists(), "sarahbeauty.db"));
            _access = new OwnerAccess(_store);
            ShowSignIn();
        }
        catch (Exception error)
        {
            var panel=Form.Panel();var status=Form.Text();
            panel.Add(Form.Title("Storage could not open"),Form.Text(error.Message),Form.Action("Review an encrypted recovery archive",status,()=>{ShowArchiveReview();return Task.CompletedTask;}),status);
            Host.Content=panel;
        }
    }

    private void ShowSignIn()
    {
        _session?.Lock();
        _session = null;
        var panel=Form.Panel();
        var recoveryStatus=Form.Text();panel.Add(Form.Text("TEST • Local business records"), OwnerView.Build(_access!, session =>
        {
            _session = session;
            ShowWorkspace();
        }),Form.Action("Review an encrypted recovery archive",recoveryStatus,()=>{ShowArchiveReview();return Task.CompletedTask;}),recoveryStatus);
        Host.Content=new ScrollViewer{Padding=new Microsoft.UI.Xaml.Thickness(24),Content=panel};
    }

    private void ShowWorkspace()
    {
        Host.Content=new WorkspaceView(_store!,_session!,ShowSignIn).Build();
    }
    private void ShowArchiveReview()
    {
        _session?.Lock();_session=null;var panel=Form.Panel();var status=Form.Text();
        if(_access is not null)panel.Add(Form.Action("Back to owner sign-in",status,()=>{ShowSignIn();return Task.CompletedTask;}),status);
        panel.Add(new RestoreReviewView().Build());Host.Content=new ScrollViewer{Padding=new Microsoft.UI.Xaml.Thickness(24),Content=panel};
    }
}
