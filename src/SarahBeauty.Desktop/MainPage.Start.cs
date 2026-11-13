using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Setup;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;
using SarahBeauty_Desktop.Views;

namespace SarahBeauty_Desktop;

public sealed partial class MainPage
{
    private void OpenStartupWorkspace()
    {
        try
        {
            if(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SARAHBEAUTY_TEST_DIRECTORY"))){OpenCurrentStore();return;}
            if(new LiveBusinesses(AppDataPaths.GetLiveFolder()).Exists){_mode=BusinessMode.Live;OpenCurrentStore();return;}
            if(File.Exists(new ActiveTestStores(AppDataPaths.GetTestFolder()).ResolveDatabase())){OpenCurrentStore();return;}
            ShowBusinessStart(BusinessMode.Live);
        }
        catch(Exception error)
        {
            var panel=Form.Panel();var status=Form.Text();panel.Add(Form.Title("Your saved workspace needs attention"),Form.Text(error.Message),WorkspaceChoiceAction(status),RecoveryActions(),status);ShowPanel(panel);
        }
    }

    private void ShowBusinessStart(BusinessMode mode)
    {
        HideOwnerContent();StopBackups();_mode=mode;_store=null;_access=null;
        int generation=_workspaceGeneration;
        var view=new BusinessStartView((name,username,password,cancellation)=>CreateStarterBusiness(mode,name,username,password,cancellation),result=>
        {
            if(generation!=_workspaceGeneration||_stoppingOwnerActivity){result.Owner.Lock();return;}
            EnterAuthenticatedStore(result.Store,result.Owner);
        });
        var panel=Form.Panel();panel.MaxWidth=480;panel.HorizontalAlignment=HorizontalAlignment.Center;
        var status=Form.Text();var advanced=Form.Panel();advanced.Add(WorkspaceChoiceAction(status),RecoveryActions(),status);
        panel.Add(view.Build(),new Expander{Header="Open an existing workspace or backup",Content=advanced,HorizontalAlignment=HorizontalAlignment.Stretch});
        ShowPanel(panel);
    }

    private (BusinessStore Store,OwnerSession Owner) CreateStarterBusiness(BusinessMode mode,string name,string username,string password,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if(mode==BusinessMode.Live)
        {
            var result=new LiveBusinesses(AppDataPaths.GetLiveFolder()).Start(name,username,password,cancellation);
            return(result.Store,result.Owner);
        }
        var store=new BusinessStore(new ActiveTestStores(AppDataPaths.EnsureTestFolderExists()).ResolveDatabase());
        var owner=new OwnerAccess(store).Start(name,username,password);
        new ChartOfAccounts(store).Initialize(owner);
        return(store,owner);
    }
}
