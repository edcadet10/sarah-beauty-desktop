using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Finance;

namespace SarahBeauty_Desktop.Views;

internal sealed class CancellationValues(CancellationSnapshot sources,List<Account> accounts,List<Booking> bookings,CancellationValueChoice[] initial)
{
    private readonly List<CancellationValueFields> _rows=[];
    private readonly StackPanel _panel=Form.Panel();
    internal UIElement Build()
    {
        var root=Form.Panel();var status=Form.Text();foreach(var value in initial)Add(value);
        root.Add(_panel,Form.Action("Add cancellation value decision",status,()=>{Add(new("","",0,0,0,0));return Task.CompletedTask;}),status);return root;
    }
    internal CancellationValueChoice[] Read()=>_rows.Select(r=>r.Read()).ToArray();
    private void Add(CancellationValueChoice value)
    {
        var fields=new CancellationValueFields(sources,accounts,bookings,value);var container=Form.Panel();var status=Form.Text();_rows.Add(fields);
        container.Add(fields.Build(),Form.Action("Remove this cancellation value decision",status,()=>{_rows.Remove(fields);_panel.Children.Remove(container);return Task.CompletedTask;}),status);_panel.Add(container);
    }
}
