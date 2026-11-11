using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class BookingView
{
    private readonly BookingBook _book;
    private readonly OwnerSession _owner;
    private readonly List<Customer> _customers;
    private readonly ListView _list = new() { MaxHeight = 220 };
    private readonly StackPanel _editor = Form.Panel();
    private readonly TextBlock _status = Form.Text();

    internal BookingView(BusinessStore store, OwnerSession owner)
    {
        (_book, _owner) = (new(store), owner);
        _customers = new CustomerBook(store).Search(owner, includeArchived: true);
    }

    internal UIElement Build()
    {
        var panel = Form.Panel();
        panel.Add(Form.Title("Bookings"), Form.Action("New wedding", _status,
            () => { Edit(BookingRules.Wedding("")); return Task.CompletedTask; }),
            Form.Action("New booking without a trial", _status,
            () => { Edit(new Booking { HasTrial = false, TrialCents = 0, EventType = "Other" }); return Task.CompletedTask; }),
            _status, _list, _editor);
        _list.SelectionChanged += (_, _) => { if (_list.SelectedItem is Booking booking) Edit(booking); };
        _list.ItemsSource = _book.List(_owner);
        return panel;
    }

    private void Edit(Booking booking)
    {
        _editor.Children.Clear();
        var fields = new BookingFields(booking, _customers);
        var status = Form.Text();
        _editor.Add(Form.Title(booking.Revision == 0 ? "New booking" : booking.Name),
            Form.Text($"Status: {booking.Status}"), fields.Build(), status,
            Form.Action("Save booking details", status, async () => await Save(fields.Read(booking))));
        if (booking.Revision == 0) return;
        _editor.Add(Summary(booking), Section("Services and charges", Charges(booking)),
            Section("Party members", Places(booking)), Section("Appointments", Appointments(booking)),
            Section("Status and history", Status(booking)));
    }

    private async Task Save(Booking booking)
    {
        var saved = await Task.Run(() => _book.Save(_owner, booking));
        _list.ItemsSource = _book.List(_owner);
        Edit(saved);
        _status.Text = $"Saved {saved.Name}.";
    }

    private static Expander Section(string title, UIElement content) =>
        new() { Header = title, Content = content, HorizontalAlignment = HorizontalAlignment.Stretch };

    private static UIElement Summary(Booking booking)
    {
        var totals = BookingRules.Totals(booking);
        string plan = totals.Plan is null ? "Payment plan needs resolved tax and an explicit plan."
            : $"Trial {Money.Display(totals.Plan.TrialCents)} • Deposit {Money.Display(totals.Plan.DepositCents)} • Final {Money.Display(totals.Plan.FinalCents)}";
        return Form.Text($"Net {Money.Display(totals.NetCents)} • Total {(totals.TotalCents.HasValue ? Money.Display(totals.TotalCents.Value) : "Tax undecided")}\n{plan}");
    }

    private UIElement Status(Booking booking)
    {
        var panel = Form.Panel();
        var reason = Form.Input("Reason for status change");
        var status = Form.Text();
        panel.Add(reason, Form.Action("Cancel booking", status, async () =>
        {
            string explanation = reason.Text;
            var changed = await Task.Run(() => _book.ChangeStatus(_owner, booking, "Cancelled", explanation));
            _list.ItemsSource = _book.List(_owner);
            Edit(changed);
        }), Form.Text(string.Join("\n", booking.History.Select(h => $"{h.AtUtc} • {h.From} → {h.To} • {h.Reason}"))), status);
        return panel;
    }

    private ComboBox CustomerSelect(string label, string id) => Form.Records(label,
        _customers.Select(c => (c.Id, $"{c.FullName} • {c.Id[..8]}")), id);
}
