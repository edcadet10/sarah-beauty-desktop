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
        catch (Exception error) { Host.Add(Form.Title("Storage could not open"), Form.Text(error.Message)); }
    }

    private void ShowSignIn()
    {
        _session?.Lock();
        _session = null;
        Host.Children.Clear();
        Host.Add(Form.Text("TEST • Local business records"), OwnerView.Build(_access!, session =>
        {
            _session = session;
            ShowWorkspace();
        }));
    }

    private void ShowWorkspace()
    {
        Host.Children.Clear();
        var status = Form.Text();
        Host.Add(Form.Title("Sarah Beauty"), Form.Text($"TEST • Signed in as {_session!.OwnerName}"),
            Form.Action("Lock / switch owner", status, () => { ShowSignIn(); return Task.CompletedTask; }),
            Form.Text("Your business is saved locally. Cloud backup has not been connected."), status);
    }
}
