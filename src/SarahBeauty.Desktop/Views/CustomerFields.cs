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
        _address = Form.MultilineInput("Billing address", customer.BillingAddress);
        _makeup = Form.MultilineInput("Makeup preferences (private)", customer.MakeupPreferences);
        _sensitivities = Form.MultilineInput("Sensitivities (private)", customer.Sensitivities);
        _notes = Form.MultilineInput("Notes (private)", customer.PrivateNotes);
        _permission = Form.Select("Photo permission", ["Not asked", "Granted", "Declined", "Withdrawn"], customer.PhotoPermission);
        _scope = Form.MultilineInput("Permission scope", customer.PermissionScope);
        _permissionNotes = Form.MultilineInput("Permission details / evidence reference", customer.PermissionNotes);
        _archive = new CheckBox { Content = "Archive this customer", IsChecked = customer.Archived };
        var permissionStatus=Form.Muted("Photo permission: "+customer.PhotoPermission);
        _permission.SelectionChanged+=(_,_)=>permissionStatus.Text="Photo permission: "+_permission.SelectedItem;
        Controls = [_name, ResponsiveGrid.Build([_phone,_email],2), _contact,
            Section("Billing details",_billingName,_address),
            Section("Preferences and private notes",_makeup,_sensitivities,_notes),permissionStatus,
            Section("Photo permission details",_permission,_scope,_permissionNotes),_archive];
    }

    private static Expander Section(string title,params UIElement[] fields)
    {
        var panel=Form.Panel();panel.Add(fields);
        return new Expander{Header=title,Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch};
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
