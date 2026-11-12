using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace SarahBeauty_Desktop.Views;

internal sealed class WorkspaceMenu(NavigationView navigation,Action<string> selectRoute)
{
    private readonly Dictionary<string,NavigationViewItem> _items=[];
    private readonly Dictionary<string,NavigationViewItem> _parents=[];

    internal void Build(IEnumerable<string> routes)
    {
        foreach(var (label,icon) in new[]{("Dashboard",Symbol.Home),("Customers",Symbol.People),("Bookings",Symbol.Edit),("Calendar",Symbol.Calendar),("Money",Symbol.Calculator)})
            navigation.MenuItems.Add(Route(label,icon));
        Group("Client documents",Symbol.Document,["Quotes","Invoices","Receipts","Credit notes","Customer credits","Apply payments","Cancellation cases","Document decisions","Discard client drafts"]);
        Group("Accounts and reports",Symbol.Calculator,["Reports","Statements","Bank matching","Reconciliation","Opening balances","Book periods","Tax periods"]);
        Group("Resources and suppliers",Symbol.Folder,["Service catalog","Resources","Evidence files","Supplier bills","Supplier credits"]);
        Group("Business settings",Symbol.Setting,["Business setup","Ownership settings","Owner security","Backups and recovery"]);
        if(!new HashSet<string>(routes).SetEquals(_items.Keys))throw new InvalidOperationException("The workspace menu must contain every section exactly once.");
        navigation.AutoSuggestBox=Search();
    }

    internal void Select(string key)
    {
        var target=_items[key];
        if(_parents.TryGetValue(key,out var parent))parent.IsExpanded=true;
        if(ReferenceEquals(navigation.SelectedItem,target))selectRoute(key);else navigation.SelectedItem=target;
    }

    private NavigationViewItem Route(string label,Symbol icon)
    {
        var item=new NavigationViewItem{Content=label,Tag=label,Icon=new SymbolIcon(icon)};
        AutomationProperties.SetName(item,label);ToolTipService.SetToolTip(item,label);
        _items.Add(label,item);return item;
    }

    private void Group(string title,Symbol icon,string[] routes)
    {
        var group=new NavigationViewItem{Content=title,Icon=new SymbolIcon(icon),SelectsOnInvoked=false};
        foreach(string route in routes){group.MenuItems.Add(Route(route,Symbol.Document));_parents.Add(route,group);}
        navigation.MenuItems.Add(group);
    }

    private AutoSuggestBox Search()
    {
        var search=new AutoSuggestBox{PlaceholderText="Find a section",QueryIcon=new SymbolIcon(Symbol.Find),MaxSuggestionListHeight=320};
        AutomationProperties.SetName(search,"Find a section");
        search.TextChanged+=(_,args)=>
        {
            if(args.Reason==AutoSuggestionBoxTextChangeReason.SuggestionChosen)return;
            search.ItemsSource=Matches(search.Text).ToArray();
        };
        search.QuerySubmitted+=(_,args)=>
        {
            string? route=args.ChosenSuggestion as string??_items.Keys.FirstOrDefault(k=>k.Equals(args.QueryText.Trim(),StringComparison.CurrentCultureIgnoreCase));
            if(route is null)return;
            Select(route);search.Text="";search.ItemsSource=null;
        };
        return search;
    }

    private IEnumerable<string> Matches(string query)=>string.IsNullOrWhiteSpace(query)?[]:
        _items.Keys.Where(k=>k.Contains(query.Trim(),StringComparison.CurrentCultureIgnoreCase)).OrderBy(k=>k);
}
