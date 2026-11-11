using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;

namespace SarahBeauty_Desktop.Views;

internal sealed class AppointmentRecipientFields
{
    private readonly SearchableRecords _place=new("Appointment recipient place");
    private readonly ListView _list=new(){MaxHeight=180};
    private readonly Dictionary<string,string> _labels;
    private readonly HashSet<string> _selected;

    internal AppointmentRecipientFields(Booking booking,Appointment appointment,List<Customer> customers)
    {
        var names=customers.ToDictionary(c=>c.Id,c=>c.FullName);var charges=booking.Charges.ToDictionary(c=>c.Id);
        _selected=appointment.PlaceIds.ToHashSet();
        _labels=booking.Places.Where(p=>p.Active || _selected.Contains(p.Id)).ToDictionary(p=>p.Id,p=>
            $"{charges[p.ChargeId].Description} • place {p.Ordinal} • {(p.CustomerId.Length==0?"Unnamed":names.GetValueOrDefault(p.CustomerId,"Customer unavailable"))}{(p.Active?"":" • Retired place")} • {p.Id[..8]}");
        _place.Update(_labels.Select(p=>(p.Key,p.Value)));Refresh();
    }

    internal UIElement Build()
    {
        var panel=Form.Panel();var status=Form.Text();
        panel.Add(Form.Text("Link actual recipient places. Leaving recipients undecided does not invent customer records or change the booked quantity."),_place.Build(),status,
            Form.Action("Add appointment recipient",status,()=>
            {
                string id=_place.SelectedId;if(id.Length==0)throw new ArgumentException("Choose a recipient place.");
                _selected.Add(id);Refresh();return Task.CompletedTask;
            }),_list,Form.Action("Remove selected appointment recipient",status,()=>
            {
                if(_list.SelectedItem is not Item selected)throw new ArgumentException("Choose a linked recipient to remove.");
                _selected.Remove(selected.Id);Refresh();return Task.CompletedTask;
            }));return panel;
    }

    private void Refresh()=>_list.ItemsSource=_selected.Select(id=>new Item(id,_labels[id])).ToArray();
    internal string[] Read()=>_selected.OrderBy(id=>id,StringComparer.Ordinal).ToArray();
    private sealed record Item(string Id,string Label){public override string ToString()=>Label;}
}
