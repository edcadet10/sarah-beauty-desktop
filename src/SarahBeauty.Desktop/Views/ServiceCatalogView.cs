using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class ServiceCatalogView
{
    private readonly ServiceCatalog _book;
    private readonly OwnerSession _owner;
    private readonly Dictionary<string,string> _names;
    private readonly TextBox _query=Form.Input("Search service code or name");
    private readonly CheckBox _archived=new(){Content="Include archived services"};
    private readonly TextBlock _status=Form.Text(),_count=Form.Text();
    private readonly ListView _list=new(){MaxHeight=220};
    private readonly StackPanel _editor=Form.Panel();
    private ServiceFields? _fields;
    private bool _refreshing,_saving;

    internal ServiceCatalogView(BusinessStore store,OwnerSession owner)
    {
        _owner=owner;_book=new(store);_book.Initialize(owner);_names=new OwnerAccess(store).Owners().ToDictionary(o=>o.Id,o=>o.Name);
    }

    internal UIElement Build()
    {
        var panel=Form.Panel();
        panel.Add(Form.Title("Service catalog"),Form.Text("Edit prices and optional durations for new booking drafts. Existing booking charges and issued documents keep their copied values."),
            _query,_archived,_count,_status,Form.Action("New catalog service",_status,async()=>{if(await ReplaceEditor())Edit(new());}),_list,_editor);
        _query.TextChanged+=(_,_)=>Refresh();_archived.Checked+=(_,_)=>Refresh();_archived.Unchecked+=(_,_)=>Refresh();
        _list.SelectionChanged+=async(_,_)=>
        {
            try{if(_refreshing || _saving || _list.SelectedItem is not Item item)return;if(await ReplaceEditor())Edit(item.Value);else _list.SelectedItem=null;}
            catch(Exception error){_status.Text=error.Message;}
        };
        Refresh();return panel;
    }

    private void Refresh()
    {
        var matches=_book.List(_owner).Where(s=>(_archived.IsChecked==true || !s.Archived) && (s.Code+" "+s.Name).Contains(_query.Text,StringComparison.OrdinalIgnoreCase)).OrderBy(s=>s.Code).ToArray();
        _count.Text=$"Showing {Math.Min(100,matches.Length)} of {matches.Length} matching services. Search to narrow the list.";
        _refreshing=true;
        try{_list.ItemsSource=matches.Take(100).Select(s=>new Item(s,$"{s.Code} • {s.Name} • {(s.UnitCents.HasValue?Money.Display(s.UnitCents.Value):"Price unknown")} / {s.Unit} • {(s.DurationMinutes.HasValue?s.DurationMinutes+" minutes":"Duration unknown")}{(s.Archived?" • Archived":"")}")).ToArray();}
        finally{_refreshing=false;}
    }

    private void Edit(ServiceDefinition service)
    {
        _editor.Children.Clear();var fields=new ServiceFields(service);_fields=fields;var status=Form.Text();
        _editor.Add(Form.Title(service.Revision==0?"New catalog service":service.Code+" • Revision "+service.Revision),fields.Build(),status,
            Form.Action("Save catalog service",status,async()=>
            {
                var value=fields.Read(service);_saving=true;
                try{var saved=await Task.Run(()=>_book.Save(_owner,value));Edit(saved);Refresh();_status.Text=$"Saved {saved.Code} revision {saved.Revision}.";}
                finally{_saving=false;}
            }),PagedReport.Build("Service price and duration history",["Revision / time","Price / unit","Duration","Tax review / evidence","Owner / state"],
                service.History.Select(h=>new[]{h.Revision+" • "+h.AtUtc+" • "+h.Name,Money.Display(h.UnitCents)+" / "+h.Unit,h.DurationMinutes.HasValue?h.DurationMinutes.Value+" minutes":"Unknown",h.TaxReviewStatus+" • "+h.TaxReviewEvidence,
                    _names.GetValueOrDefault(h.OwnerId,"Former owner")+" • "+(h.Archived?"Archived":"Active")})));
    }

    private async Task<bool> ReplaceEditor()
    {
        if(_saving)throw new ArgumentException("Wait for the current service save to finish.");
        if(_fields?.IsDirty!=true)return true;
        var dialog=new ContentDialog{XamlRoot=_editor.XamlRoot,Title="Replace unsaved service changes?",Content="The current service fields have unsaved changes.",PrimaryButtonText="Discard changes",CloseButtonText="Keep editing",DefaultButton=ContentDialogButton.Close};
        return await dialog.ShowAsync()==ContentDialogResult.Primary;
    }
    private sealed record Item(ServiceDefinition Value,string Label){public override string ToString()=>Label;}
}
