using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class ResourceView
{
    private void Edit(Supply supply)
    {
        _detail.Children.Clear();
        var name=Form.Input("Supply / product name",supply.Name);
        var brand=Form.Input("Brand",supply.Brand);
        var shade=Form.Input("Shade",supply.Shade);
        var opened=Form.Input("Opened date (YYYY-MM-DD; optional)",supply.OpenedDate?.ToString("yyyy-MM-dd")??"");
        var expiry=Form.Input("Expiry date (optional)",supply.ExpiryDate?.ToString("yyyy-MM-dd")??"");
        var state=Form.Select("Supply status",["Available","Low","Finished"],supply.SupplyStatus);
        var location=Form.Input("Storage location",supply.Location);
        var purchase=Form.Input("Purchase reference",supply.PurchaseReference);
        var notes=Form.MultilineInput("Private notes",supply.Notes);
        var archived=Archived(supply.Archived);
        var status=Form.Text();
        Supply Read()=>supply with{Name=name.Text,Brand=brand.Text,Shade=shade.Text,OpenedDate=Input.Date(opened.Text),ExpiryDate=Input.Date(expiry.Text),
                    SupplyStatus=(string)state.SelectedItem,Location=location.Text,PurchaseReference=purchase.Text,Notes=Form.ReadInput(notes,supply.Notes),Archived=archived.IsChecked==true};
        TrackDraft(supply,Read);
        _detail.Add(Form.Title("Supply"),Form.Text("Status and expiry tracking do not estimate consumption or create an expense. Record purchases separately."),
            name,brand,shade,opened,expiry,state,location,purchase,notes,archived,status,Form.Action("Save supply",status,async()=>
            {
                var value=Read();
                await SaveEntry(()=>_operations.SaveSupply(owner,value),Edit);
            }),Form.Text(string.Join("\n",supply.History.Select(h=>$"{h.AtUtc} • {h.Status} • Opened {h.OpenedDate:yyyy-MM-dd} • Expiry {h.ExpiryDate:yyyy-MM-dd}"))));
    }
}
