using Microsoft.UI.Xaml;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class BookingView
{
    private UIElement CatalogServices(Booking booking)
    {
        var panel=Form.Panel();var picker=new SearchableRecords("Catalog service to add");var quantity=Form.Input("Catalog service quantity","1");var status=Form.Text();
        var services=_catalog.List(_owner).Where(s=>!s.Archived && s.Kind=="Service").ToDictionary(s=>s.Id);
        picker.Update(services.Values.Select(s=>(s.Id,$"{s.Code} • {s.Name} • {(s.UnitCents.HasValue?Money.Display(s.UnitCents.Value):"Price unknown")} / {s.Unit} • {(s.DurationMinutes.HasValue?s.DurationMinutes+" minutes":"Duration unknown")}")));
        panel.Add(Form.Title("Add a catalog service"),picker.Build(),quantity,status,Form.Action("Add selected catalog service",status,async()=>
        {
            if(!services.TryGetValue(picker.SelectedId,out var selected))throw new ArgumentException("Choose an active catalog service.");
            var changed=_catalog.AddService(_owner,booking,selected.Id,Input.Decimal(quantity.Text),selected.Revision);
            await Save(changed);
        }));
        return panel;
    }
}
