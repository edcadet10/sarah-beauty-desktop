using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class BookingView
{
    private UIElement Charges(Booking booking)
    {
        var panel = Form.Panel();
        foreach (var charge in booking.Charges.Where(c => c.Active))
            panel.Add(Section(charge.ToString(), ChargeEditor(booking, charge)));
        panel.Add(CatalogServices(booking));
        var description = Form.Input("New service description");
        var unit = Form.Select("Unit", ["Person", "Package", "Mile", "Custom"], "Person");
        var quantity = Form.Input("Quantity", "1");
        var price = Form.Input("Unit price in USD", "120.00");
        var status = Form.Text();
        panel.Add(description, unit, quantity, price, Form.Action("Add service", status, async () =>
            await Save(BookingRules.AddCharge(booking, description.Text, (string)unit.SelectedItem,
                Input.Decimal(quantity.Text), Money.Parse(price.Text)))), status);
        return panel;
    }

    private UIElement ChargeEditor(Booking booking, BookingCharge charge)
    {
        var panel = Form.Panel();
        var description = Form.Input("Description", charge.Description);
        var quantity = Form.Input("Quantity (zero removes this charge)", charge.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var price = Form.Input("Unit price in USD", Money.Input(charge.UnitCents));
        var discount = Form.Input("Discount in USD", Money.Input(charge.DiscountCents));
        var tax = Form.Input("Resolved tax rate (decimal, e.g. 0.075; blank means undecided)", charge.TaxRate?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "");
        var payer = CustomerSelect("Default payer for this charge (travel payer must be explicit)", charge.PayerCustomerId);
        var duration=Form.Input("Duration for this service in minutes (blank means unknown)",charge.DurationMinutes?.ToString()??"");
        var status = Form.Text();
        if(charge.ServiceId.Length>0)panel.Add(Form.Text("Copied catalog revision: "+charge.ServiceRevision));
        panel.Add(description, quantity, price, discount, tax, payer,duration, Form.Action("Save charge", status, async () =>
        {
            var changed = BookingRules.ChangeQuantity(booking, charge.Id, Input.Decimal(quantity.Text));
            var edited = changed.Charges.Single(c => c.Id == charge.Id) with
            {
                Description = description.Text.Trim(), UnitCents = Money.Parse(price.Text), DiscountCents = Money.Parse(discount.Text),
                TaxRate = string.IsNullOrWhiteSpace(tax.Text) ? null : Input.Decimal(tax.Text), PayerCustomerId = payer.SelectedId(),DurationMinutes=Input.Minutes(duration.Text)
            };
            await Save(changed with { Charges = changed.Charges.Select(c => c.Id == charge.Id ? edited : c).ToArray() });
        }), status);
        return panel;
    }

    private UIElement Places(Booking booking)
    {
        var panel = Form.Panel();
        var active = booking.Places.Where(p => p.Active).ToArray();
        panel.Add(Form.Text($"Total {active.Length} • Named {active.Count(p => p.CustomerId.Length > 0)} • Unnamed {active.Count(p => p.CustomerId.Length == 0)}"));
        foreach (var place in active)
        {
            var customer = CustomerSelect($"{booking.Charges.Single(c => c.Id == place.ChargeId).Description} — place {place.Ordinal}", place.CustomerId);
            var status = Form.Text();
            panel.Add(customer, Form.Action("Link / unlink this recipient", status, async () =>
                await Save(booking with { Places = booking.Places.Select(p => p.Id == place.Id ? p with { CustomerId = customer.SelectedId() } : p).ToArray() })), status);
        }
        return panel;
    }
}
