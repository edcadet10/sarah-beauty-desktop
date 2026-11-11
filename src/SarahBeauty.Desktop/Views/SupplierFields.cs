using SarahBeauty.Business.Resources;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class ResourceView
{
    private void Edit(Supplier supplier)
    {
        _detail.Children.Clear();
        var name=Form.Input("Supplier name",supplier.Name);
        var phone=Form.Input("Phone (optional)",supplier.Phone);
        var email=Form.Input("Email (optional)",supplier.Email);
        var address=Form.Input("Address",supplier.Address);
        var terms=Form.Input("Payment terms",supplier.Terms);
        var notes=Form.Input("Private supplier notes",supplier.PrivateNotes);
        var archived=Archived(supplier.Archived);
        var status=Form.Text();
        _detail.Add(Form.Title("Supplier"),name,phone,email,address,terms,notes,archived,status,
            Form.Action("Save supplier",status,async()=>
            {
                var value=supplier with{Name=name.Text,Phone=phone.Text.Trim(),Email=email.Text.Trim(),Address=address.Text.Trim(),
                    Terms=terms.Text,PrivateNotes=notes.Text,Archived=archived.IsChecked==true};
                var saved=await Task.Run(()=>_book.SaveSupplier(owner,value));
                Refresh();
                Edit(saved);
            }));
    }
}
