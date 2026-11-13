using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class WorkspaceView(BusinessStore store,OwnerSession session,Action locked,Func<Task> requestLock,Action? retired=null,Func<string>? backupRuntime=null,Func<string>? googleRuntime=null,Func<string>? sheetsRuntime=null,Action? passphraseChanged=null)
{
    private readonly ContentControl _content=new(){HorizontalContentAlignment=HorizontalAlignment.Stretch};
    private readonly Dictionary<string,UIElement> _pages=[];
    private readonly TextBlock _status=Form.Text();
    private readonly TextBlock _businessTitle=Form.Title("");
    private string _current="Dashboard";
    private readonly Dictionary<string,Func<UIElement>> _routes=new();
    private NavigationView? _navigation;
    private WorkspaceMenu? _menu;
    private OwnerSecurityView? _ownerSecurity;
    private DocumentDecisionView? _documentDecisions;
    private BusinessProfileView? _businessProfile;
    private ClientDraftDiscardView? _draftDiscards;

    internal UIElement Build()
    {
        Routes();
        var root=new Grid();
        root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        var header=Form.Panel();
        header.Margin=new Thickness(24,12,24,8);
        _businessTitle.Text=store.BusinessName(session);header.Add(_businessTitle,Form.Text($"{BusinessModes.StoredName(store.Mode)} • Signed in as {session.OwnerName}"),_status);
        var nav=new NavigationView{IsSettingsVisible=false,PaneDisplayMode=NavigationViewPaneDisplayMode.Auto,IsPaneOpen=true,
            OpenPaneLength=256,IsBackButtonVisible=NavigationViewBackButtonVisible.Collapsed,PaneTitle="Workspace"};
        _navigation=nav;
        _menu=new WorkspaceMenu(nav,Navigate);_menu.Build(_routes.Keys);
        nav.Content=new ScrollViewer{Padding=new Thickness(24),HorizontalContentAlignment=HorizontalAlignment.Stretch,Content=_content};
        nav.PaneFooter=Footer(root);
        nav.SelectionChanged+=(_,args)=>
        {
            try{if(args.SelectedItemContainer?.Tag is string key)Navigate(key);}
            catch(Exception error){_status.Text=error.Message;}
        };
        Grid.SetRow(nav,1);
        root.Children.Add(header);
        root.Children.Add(nav);
        _menu.Select("Dashboard");
        return root;
    }

    private void Routes()
    {
        _routes["Dashboard"]=()=>new DashboardView(store,session,SelectRoute).Build();
        _routes["Customers"]=()=>new CustomerView(store,session).Build();
        _routes["Bookings"]=()=>new BookingView(store,session).Build();
        _routes["Calendar"]=()=>new ScheduleView(store,session).Build();
        _routes["Cancellation cases"]=()=>new CancellationView(store,session).Build();
        _routes["Service catalog"]=()=>new ServiceCatalogView(store,session).Build();
        _routes["Quotes"]=()=>new QuoteView(store,session).Build();
        _routes["Invoices"]=()=>new InvoiceView(store,session).Build();
        _routes["Credit notes"]=()=>new CreditNoteView(store,session).Build();
        _routes["Receipts"]=()=>new ReceiptView(store,session).Build();
        _routes["Document decisions"]=BuildDocumentDecisions;
        _routes["Discard client drafts"]=BuildDraftDiscards;
        _routes["Money"]=()=>new MoneyView(store,session).Build();
        _routes["Apply payments"]=()=>new AllocationView(store,session).Build();
        _routes["Customer credits"]=()=>new CreditView(store,session).Build();
        _routes["Resources"]=()=>new ResourceView(store,session).Build();
        _routes["Evidence files"]=()=>new EvidenceView(store,session).Build();
        _routes["Backups and recovery"]=()=>new LocalBackupView(store,session,retired,backupRuntime,googleRuntime,sheetsRuntime).Build();
        _routes["Opening balances"]=()=>new OpeningView(store,session).Build();
        _routes["Business setup"]=BuildBusinessProfile;
        _routes["Ownership settings"]=()=>new OwnerInterestView(store,session).Build();
        _routes["Owner security"]=BuildOwnerSecurity;
        _routes["Reports"]=()=>new ReportsView(store,session).Build();
        _routes["Statements"]=()=>new StatementPreviewView(store,session).Build();
        _routes["Bank matching"]=()=>new BankMatchingView(store,session).Build();
        _routes["Reconciliation"]=()=>new ReconciliationView(store,session).Build();
        _routes["Book periods"]=()=>new BookPeriodView(store,session).Build();
        _routes["Tax periods"]=()=>new TaxPeriodView(store,session).Build();
        _routes["Supplier bills"]=()=>new PurchaseView(store,session).Build();
        _routes["Supplier credits"]=()=>new PurchaseCreditView(store,session).Build();
    }

    private void Navigate(string key)
    {
        if(_current=="Owner security")_ownerSecurity?.Leave();
        if(_current=="Document decisions")_documentDecisions?.Leave();
        if(_current=="Business setup")_businessProfile?.Leave();
        if(_current=="Discard client drafts")_draftDiscards?.Leave();
        if(!_pages.TryGetValue(key,out var page)){page=_routes[key]();_pages[key]=page;}
        _content.Content=page;
        _current=key;
        _status.Text="Local records";
        if(_navigation is not null&&_navigation.IsLoaded&&_navigation.DisplayMode!=NavigationViewDisplayMode.Expanded)_navigation.IsPaneOpen=false;
    }
    private UIElement BuildOwnerSecurity(){_ownerSecurity=new(store,session,passphraseChanged??locked);return _ownerSecurity.Build();}
    private UIElement BuildDocumentDecisions(){_documentDecisions=new(store,session);return _documentDecisions.Build();}
    private UIElement BuildBusinessProfile(){_businessProfile=new(store,session,name=>_businessTitle.Text=name);return _businessProfile.Build();}
    private UIElement BuildDraftDiscards(){_draftDiscards=new(store,session);return _draftDiscards.Build();}

    private UIElement Footer(FrameworkElement root)
    {
        var panel=Form.Panel();
        panel.Add(Form.Action("Reload this section",_status,async()=>
        {
            var dialog=new ContentDialog{XamlRoot=root.XamlRoot,Title="Reload this section?",
                Content="Unsaved fields in this section will be discarded. Other sections keep their unsaved work.",
                PrimaryButtonText="Reload",CloseButtonText="Keep editing",DefaultButton=ContentDialogButton.Close};
            if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;
            _pages.Remove(_current);
            Navigate(_current);
        }),Form.Action("Lock / switch owner",_status,requestLock));
        return panel;
    }

    private void SelectRoute(string key)
    {
        _menu?.Select(key);
    }
}
