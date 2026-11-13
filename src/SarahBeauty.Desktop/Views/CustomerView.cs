using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class CustomerView
{
    private readonly CustomerBook _book;
    private readonly OwnerSession _session;
    private readonly TextBox _search = Form.Input("Search by customer name");
    private readonly CheckBox _archived = new() { Content = "Include archived customers" };
    private readonly ListView _list = new() { MaxHeight = 240, SelectionMode = ListViewSelectionMode.Single };
    private readonly StackPanel _editor = Form.Panel();
    private readonly TextBlock _status = Form.Text();

    internal CustomerView(BusinessStore store, OwnerSession session) => (_book, _session) = (new(store), session);

    internal UIElement Build()
    {
        var panel = Form.Panel();
        panel.Add(Form.Title("Customers"), _search, _archived,
            Form.Action("Search", _status, Refresh),
            Form.Action("New customer", _status, () => { Edit(new Customer()); return Task.CompletedTask; }),
            _status, _list, _editor);
        _list.SelectionChanged += (_, _) => { if (_list.SelectedItem is CustomerListChoice choice) Edit(choice.Value); };
        Show(_book.Search(_session));
        Edit(new Customer());
        return panel;
    }

    private async Task Refresh()
    {
        var (search, archived) = (_search.Text, _archived.IsChecked == true);
        Show(await Task.Run(() => _book.Search(_session, search, archived)));
    }

    private void Show(IEnumerable<Customer> customers)=>_list.ItemsSource=customers.Select(c=>new CustomerListChoice(c)).ToList();

    private void Edit(Customer customer)
    {
        _editor.Children.Clear();
        var fields = new CustomerFields(customer);
        var status = Form.Text(customer.ContactWarning);
        _editor.Add(Form.Title(customer.Id.Length == 0 ? "New customer" : "Edit customer"));
        foreach (var control in fields.Controls) _editor.Children.Add(control);
        _editor.Add(status, Form.Action("Save customer", status, async () =>
        {
            var value = fields.Read(customer);
            var saved = await Task.Run(() => _book.Save(_session, value));
            await Refresh();
            Edit(saved);
            _status.Text = $"Saved {saved.FullName}. {saved.ContactWarning}";
        }));
        if (customer.Id.Length > 0)
            _editor.Add(Form.Text("Photo permission history"), Form.Text(string.Join("\n",
                _book.Permissions(_session, customer.Id).Select(p => $"{p.AtUtc} • {p.Status} • {p.Scope} • {p.Notes}"))));
    }
}
