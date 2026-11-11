using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Customers;

namespace SarahBeauty_Desktop.Views;

internal sealed class CustomerFields
{
    private readonly TextBox _name, _phone, _email, _billingName, _address, _makeup, _sensitivities, _notes, _scope, _permissionNotes;
    private readonly ComboBox _contact, _permission;
    private readonly CheckBox _archive;
    internal UIElement[] Controls { get; }

    internal CustomerFields(Customer customer)
    {
        _name = Form.Input("Full name (required)", customer.FullName);
        _phone = Form.Input("Phone (optional)", customer.Phone);
        _email = Form.Input("Email (optional)", customer.Email);
        _contact = Form.Select("Preferred contact", ["Undecided", "Phone", "Text", "Email"], customer.PreferredContact);
        _billingName = Form.Input("Billing name", customer.BillingName);
        _address = Form.Input("Billing address", customer.BillingAddress);
        _makeup = Form.Input("Makeup preferences (private)", customer.MakeupPreferences);
        _sensitivities = Form.Input("Sensitivities (private)", customer.Sensitivities);
        _notes = Form.Input("Notes (private)", customer.PrivateNotes);
        _permission = Form.Select("Photo permission", ["Not asked", "Granted", "Declined", "Withdrawn"], customer.PhotoPermission);
        _scope = Form.Input("Permission scope", customer.PermissionScope);
        _permissionNotes = Form.Input("Permission details / evidence reference", customer.PermissionNotes);
        _archive = new CheckBox { Content = "Archive this customer", IsChecked = customer.Archived };
        Controls = [_name, _phone, _email, _contact, _billingName, _address, _makeup, _sensitivities, _notes,
            _permission, _scope, _permissionNotes, _archive];
    }

    internal Customer Read(Customer previous) => previous with
    {
        FullName = _name.Text, Phone = _phone.Text, Email = _email.Text,
        PreferredContact = (string)_contact.SelectedItem, BillingName = _billingName.Text,
        BillingAddress = _address.Text, MakeupPreferences = _makeup.Text, Sensitivities = _sensitivities.Text,
        PrivateNotes = _notes.Text, PhotoPermission = (string)_permission.SelectedItem,
        PermissionScope = _scope.Text, PermissionNotes = _permissionNotes.Text, Archived = _archive.IsChecked == true
    };
}
