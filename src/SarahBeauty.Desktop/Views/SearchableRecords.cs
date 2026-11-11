using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SarahBeauty_Desktop.Views;

internal sealed class SearchableRecords(string label)
{
    private readonly ComboBox _choice=new(){Header=label,MinWidth=200};
    private readonly TextBox _search=Form.Input("Search "+label.ToLowerInvariant());
    private (string Id,string Label)[] _choices=[];

    internal UIElement Build()
    {
        var panel=Form.Panel();panel.Add(_search,_choice);
        _search.TextChanged+=(_,_)=>Filter();return panel;
    }

    internal string SelectedId=>_choice.SelectedId();
    internal void Update(IEnumerable<(string Id,string Label)> choices,string? selected=null){_choices=choices.ToArray();Filter(selected);}

    private void Filter(string? requested=null)
    {
        string selected=requested??SelectedId;
        var matches=_choices.Where(c=>c.Label.Contains(_search.Text,StringComparison.OrdinalIgnoreCase)).Take(50).ToList();
        var kept=_choices.FirstOrDefault(c=>c.Id==selected);
        if(kept.Id is not null && matches.All(c=>c.Id!=selected))matches.Add(kept);
        var source=Form.Records(label,matches,selected,"Choose a source");
        _choice.ItemsSource=source.ItemsSource;_choice.SelectedItem=source.SelectedItem;
    }
}
