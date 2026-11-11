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
            var panel=Form.Panel();
            panel.Add(Form.Title("Storage could not open"),Form.Text(error.Message));
            Host.Content=panel;
        }
    }

    private void ShowSignIn()
    {
        _session?.Lock();
        _session = null;
        var panel=Form.Panel();
        panel.Add(Form.Text("TEST • Local business records"), OwnerView.Build(_access!, session =>
        {
            _session = session;
            ShowWorkspace();
        }));
        Host.Content=new ScrollViewer{Padding=new Microsoft.UI.Xaml.Thickness(24),Content=panel};
    }

    private void ShowWorkspace()
    {
        Host.Content=new WorkspaceView(_store!,_session!,ShowSignIn).Build();
    }
}
