using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class BookingFields
{
    private readonly TextBox _name, _date, _venue, _ready, _responsibility, _trial, _deposit, _notes, _trialDue, _depositDue, _finalDue;
    private readonly ComboBox _customer, _payers;
    private readonly CheckBox _hasTrial;

    internal BookingFields(Booking booking, List<Customer> customers)
    {
        _name = Form.Input("Booking name", booking.Name);
        _date = Form.Input("Event date (YYYY-MM-DD; optional in an inquiry)", booking.EventDate?.ToString("yyyy-MM-dd") ?? "");
        _venue = Form.Input("Venue and address", booking.Venue);
        _ready = Form.Input("Ready-by time (America/New_York)", booking.ReadyBy);
        _customer = Form.Records("Primary contact", customers.Select(c => (c.Id, $"{c.FullName} • {c.Id[..8]}")), booking.PrimaryCustomerId);
        _payers = Form.Select("Payer arrangement", ["Undecided", "Single", "Split"], booking.PayerMode);
        _responsibility = Form.Input("Responsibility for unpaid shares (record the agreed decision)", booking.UnpaidResponsibility);
        _hasTrial = new CheckBox { Content = "This booking includes a trial", IsChecked = booking.HasTrial };
        _trial = Form.Input("Trial portion in USD (part of the agreed total)", Money.Input(booking.TrialCents));
        _deposit = Form.Input("Deposit in USD for a booking without a trial", booking.CustomDepositCents.HasValue ? Money.Input(booking.CustomDepositCents.Value) : "");
        _trialDue = Form.Input("Trial due date", booking.TrialDue?.ToString("yyyy-MM-dd") ?? "");
        _depositDue = Form.Input("Deposit due date", booking.DepositDue?.ToString("yyyy-MM-dd") ?? "");
        _finalDue = Form.Input("Final due date", booking.FinalDue?.ToString("yyyy-MM-dd") ?? "");
        _notes = Form.Input("Booking notes (private)", booking.PrivateNotes);
    }

    internal UIElement Build()
    {
        var panel = Form.Panel();
        panel.Add(_name, _date, _venue, _ready, _customer, _payers, _responsibility, _hasTrial,
            _trial, _deposit, _trialDue, _depositDue, _finalDue, _notes);
        return panel;
    }

    internal Booking Read(Booking previous) => previous with
    {
        Name = _name.Text, EventDate = Input.Date(_date.Text), Venue = _venue.Text.Trim(), ReadyBy = _ready.Text.Trim(),
        PrimaryCustomerId = _customer.SelectedId(), PayerMode = (string)_payers.SelectedItem,
        UnpaidResponsibility = _responsibility.Text.Trim(), HasTrial = _hasTrial.IsChecked == true,
        TrialCents = Money.Parse(_trial.Text), CustomDepositCents = string.IsNullOrWhiteSpace(_deposit.Text) ? null : Money.Parse(_deposit.Text),
        TrialDue = Input.Date(_trialDue.Text), DepositDue = Input.Date(_depositDue.Text), FinalDue = Input.Date(_finalDue.Text),
        PrivateNotes = _notes.Text.Trim()
    };
}
