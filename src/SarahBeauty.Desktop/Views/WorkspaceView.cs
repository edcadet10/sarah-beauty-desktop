using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class WorkspaceView(BusinessStore store,OwnerSession session,Action locked)
{
    private readonly ContentControl _content=new(){HorizontalContentAlignment=HorizontalAlignment.Stretch};
    private readonly Dictionary<string,UIElement> _pages=[];
    private readonly TextBlock _status=Form.Text();
    private string _current="Dashboard";
    private readonly Dictionary<string,Func<UIElement>> _routes=new();

    internal UIElement Build()
    {
        Routes();
        var root=new Grid();
        root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        var header=Form.Panel();
        header.Margin=new Thickness(24,12,24,8);
        header.Add(Form.Title(store.BusinessName(session)),Form.Text($"TEST • Signed in as {session.OwnerName}"),_status);
        var nav=new NavigationView{IsSettingsVisible=false,PaneDisplayMode=NavigationViewPaneDisplayMode.Auto,IsPaneOpen=true};
        foreach(string label in _routes.Keys)nav.MenuItems.Add(new NavigationViewItem{Content=label,Tag=label,Icon=new SymbolIcon(Symbol.Document)});
        nav.Content=new ScrollViewer{Padding=new Thickness(24),Content=_content};
        nav.PaneFooter=Footer(root);
        nav.SelectionChanged+=(_,args)=>
        {
            try{if(args.SelectedItemContainer?.Tag is string key)Navigate(key);}
            catch(Exception error){_status.Text=error.Message;}
        };
        Grid.SetRow(nav,1);
        root.Children.Add(header);
        root.Children.Add(nav);
        nav.SelectedItem=nav.MenuItems[0];
        Navigate("Dashboard");
        return root;
    }

    private void Routes()
    {
        _routes["Dashboard"]=Dashboard;
        _routes["Customers"]=()=>new CustomerView(store,session).Build();
        _routes["Bookings"]=()=>new BookingView(store,session).Build();
        _routes["Calendar"]=()=>new ScheduleView(store,session).Build();
        _routes["Cancellation cases"]=()=>new CancellationView(store,session).Build();
        _routes["Service catalog"]=()=>new ServiceCatalogView(store,session).Build();
        _routes["Quotes"]=()=>new QuoteView(store,session).Build();
        _routes["Invoices"]=()=>new InvoiceView(store,session).Build();
        _routes["Credit notes"]=()=>new CreditNoteView(store,session).Build();
        _routes["Receipts"]=()=>new ReceiptView(store,session).Build();
        _routes["Money"]=()=>new MoneyView(store,session).Build();
        _routes["Apply payments"]=()=>new AllocationView(store,session).Build();
        _routes["Customer credits"]=()=>new CreditView(store,session).Build();
        _routes["Resources"]=()=>new ResourceView(store,session).Build();
        _routes["Opening balances"]=()=>new OpeningView(store,session).Build();
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
        if(!_pages.TryGetValue(key,out var page)){page=_routes[key]();_pages[key]=page;}
        _content.Content=page;
        _current=key;
        _status.Text="Local records • Cloud not connected";
    }

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
        }),Form.Action("Lock / switch owner",_status,()=>{locked();return Task.CompletedTask;}));
        return panel;
    }

    private UIElement Dashboard()
    {
        var panel=Form.Panel();
        panel.Add(Form.Title("Dashboard"),Form.Text($"Active customers: {new CustomerBook(store).Search(session).Count}"),
            Form.Text("Cloud export: Never completed. Recovery upload: Never completed."));
        return panel;
    }
}
