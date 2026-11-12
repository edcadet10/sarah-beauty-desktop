using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class CustomerView
{
    private readonly CustomerBook _book;
    private readonly OwnerSession _session;
    private readonly TextBox _search = Form.Input("Search by customer name");
    private readonly CheckBox _archived = new() { Content = "Include archived customers" };
    private readonly ListView _list = new() { MaxHeight = 420, SelectionMode = ListViewSelectionMode.Single };
    private readonly StackPanel _editor = Form.Panel();
    private readonly StackPanel _browser = Form.Panel();
    private readonly ContentControl _workspace = new(){HorizontalContentAlignment=HorizontalAlignment.Stretch};
    private readonly TextBlock _status = Form.Text();
    private Customer? _editing;
    private CustomerFields? _fields;
    private ContentDialog? _discardDialog;
    private bool _restoringSelection,_saving;

    internal CustomerView(BusinessStore store, OwnerSession session) => (_book, _session) = (new(store), session);

    internal UIElement Build()
    {
        _list.ItemTemplate=(DataTemplate)Application.Current.Resources["CustomerRowTemplate"];
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_list,"Customer list");
        _list.ContainerContentChanging+=(_,args)=>
        {
            if(args.InRecycleQueue||args.Item is not CustomerListChoice choice)return;
            args.ItemContainer.HorizontalContentAlignment=HorizontalAlignment.Stretch;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(args.ItemContainer,choice.ToString());
        };
        var panel = Form.Panel();var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};
        actions.Add(Form.Action("Search",_status,Refresh),Form.PrimaryAction("New customer",_status,NewCustomer));
        _browser.Add(Form.Text("Find a customer"),_search,_archived,actions,_status,_list);
        _workspace.Content=Workspace(_browser);
        panel.Add(Form.Title("Customers"),Form.Muted("Find an existing customer or start a new record."),_workspace);
        _list.SelectionChanged += async (_, _) =>
        {
            try{await SelectCustomer();}catch(Exception error){_status.Text=error.Message;}
        };
        panel.Unloaded+=(_,_)=>_discardDialog?.Hide();
        Show(_book.Search(_session));
        _editor.Add(Form.Title("Customer details"),Form.Muted("Choose a customer from the list, or select New customer."));
        return panel;
    }

    private async Task NewCustomer()
    {
        if(!await ConfirmDiscard())return;
        SelectRow(null);Edit(new Customer());
    }

    private async Task SelectCustomer()
    {
        if(_restoringSelection||_list.SelectedItem is not CustomerListChoice choice)return;
        if(_editing?.Id==choice.Value.Id)return;
        SelectRow(_editing?.Id);
        if(!await ConfirmDiscard())return;
        Edit(choice.Value);SelectRow(choice.Value.Id);
    }

    private void SelectRow(string? id)
    {
        _restoringSelection=true;
        try{_list.SelectedItem=_list.Items.OfType<CustomerListChoice>().FirstOrDefault(c=>c.Value.Id==id);}
        finally{_restoringSelection=false;}
    }

    private async Task<bool> ConfirmDiscard()
    {
        if(_saving||_discardDialog is not null)return false;
        if(_editing is null||_fields is null||_fields.Read(_editing)==_editing)return true;
        var dialog=new ContentDialog{XamlRoot=_editor.XamlRoot,Title="Discard unsaved customer changes?",
            Content="Your customer changes have not been saved. Discard them to continue.",PrimaryButtonText="Discard changes",
            CloseButtonText="Keep editing",DefaultButton=ContentDialogButton.Close};
        _discardDialog=dialog;
        try{return await dialog.ShowAsync()==ContentDialogResult.Primary;}
        finally{_discardDialog=null;}
    }

    private UIElement Workspace(UIElement browser)
    {
        var grid=new Grid{ColumnSpacing=20,RowSpacing=20};
        var browse=Form.Card(browser);var edit=Form.Card(_editor);
        browse.VerticalAlignment=edit.VerticalAlignment=VerticalAlignment.Top;
        grid.Children.Add(browse);grid.Children.Add(edit);
        ArrangeWorkspace(grid,edit,0);
        grid.SizeChanged+=(_,args)=>ArrangeWorkspace(grid,edit,args.NewSize.Width);
        return grid;
    }

    private static void ArrangeWorkspace(Grid grid,FrameworkElement editor,double width)
    {
        bool wide=width>=860;int columns=wide?2:1;
        if(grid.ColumnDefinitions.Count==columns)return;
        grid.ColumnDefinitions.Clear();grid.RowDefinitions.Clear();
        if(wide)grid.ColumnDefinitions.Add(new(){Width=new GridLength(320)});
        grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
        if(!wide)grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
        Grid.SetColumn(editor,wide?1:0);Grid.SetRow(editor,wide?0:1);
    }

    private async Task Refresh()
    {
        var (search, archived) = (_search.Text, _archived.IsChecked == true);
        Show(await Task.Run(() => _book.Search(_session, search, archived)));
    }

    private void Show(IEnumerable<Customer> customers)=>_list.ItemsSource=customers.Select(c=>new CustomerListChoice(c)).ToList();

    private void Edit(Customer customer)
    {
        _editor.Children.Clear();
        var fields = new CustomerFields(customer);
        _editing=customer;_fields=fields;
        var status = Form.Text(customer.ContactWarning);
        _editor.Add(Form.Title(customer.Id.Length == 0 ? "New customer" : "Edit customer"));
        foreach (var control in fields.Controls) _editor.Children.Add(control);
        _editor.Add(status, Form.PrimaryAction("Save customer", status,()=>SaveCustomer(customer,fields)));
        if (customer.Id.Length > 0)
            _editor.Add(Form.Text("Photo permission history"), Form.Text(string.Join("\n",
                _book.Permissions(_session, customer.Id).Select(p => $"{p.AtUtc} • {p.Status} • {p.Scope} • {p.Notes}"))));
    }

    private async Task SaveCustomer(Customer customer,CustomerFields fields)
    {
        if(_saving)throw new ArgumentException("Wait for the customer save to finish.");
        var value=fields.Read(customer);_saving=true;_workspace.IsEnabled=false;
        _status.Text="Saving customer…";
        try
        {
            var saved=await Task.Run(()=>_book.Save(_session,value));
            await Refresh();Edit(saved);SelectRow(saved.Id);
            _status.Text=$"Saved {saved.FullName}. {saved.ContactWarning}";
        }
        catch{_status.Text="";throw;}
        finally{_saving=false;_workspace.IsEnabled=true;}
    }
}
