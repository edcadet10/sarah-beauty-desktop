using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class BookingView
{
    private UIElement Appointments(Booking booking)
    {
        var panel = Form.Panel();
        var name = Form.Input("Artist name");
        var status = Form.Text();
        panel.Add(name, Form.Action("Add artist", status, async () =>
        {
            string artistName = name.Text;
            await Task.Run(() => _book.SaveArtist(_owner, new Artist { Name = artistName }));
            Edit(booking);
        }), status);
        foreach (var appointment in booking.Appointments)
            panel.Add(Section($"{appointment.Kind} • {appointment.Start?.ToString("g") ?? "Time undecided"}", AppointmentEditor(booking, appointment)));
        panel.Add(Section("New appointment", AppointmentEditor(booking,
            new Appointment(Guid.NewGuid().ToString(), booking.HasTrial ? "Trial" : "Other", "", null, null, ""))));
        return panel;
    }

    private UIElement AppointmentEditor(Booking booking, Appointment appointment)
    {
        var panel = Form.Panel();
        var kind = Form.Select("Appointment kind", ["Trial", "Wedding", "Other"], appointment.Kind);
        var artist = Form.Records("Artist", _book.Artists(_owner).Where(a => !a.Archived).Select(a => (a.Id, a.Name)), appointment.ArtistId);
        var start = Form.Input("Start (YYYY-MM-DDTHH:MM-05:00 or -04:00)", appointment.Start?.ToString("yyyy-MM-dd'T'HH:mmzzz") ?? "");
        var end = Form.Input("End (include the correct UTC offset)", appointment.End?.ToString("yyyy-MM-dd'T'HH:mmzzz") ?? "");
        var location = Form.Input("Appointment location", appointment.Location);
        var state = Form.Select("Status", ["Planned", "Completed", "Cancelled"], appointment.Status);
        var reason = Form.Input("Overlap override reason (only if needed)", appointment.OverrideReason);
        var products = Form.Input("Products and shades used (private)", appointment.ProductsAndShades);
        var notes = Form.Input("Appointment notes (private)", appointment.PrivateNotes);
        var status = Form.Text();
        panel.Add(kind, artist, start, end, location, state, reason, products, notes,
            Form.Action("Save appointment", status, async () =>
            {
                var saved = appointment with { Kind = (string)kind.SelectedItem, ArtistId = artist.SelectedId(),
                    Start = Input.Time(start.Text), End = Input.Time(end.Text), Location = location.Text,
                    Status = (string)state.SelectedItem, OverrideReason = reason.Text.Trim(), ProductsAndShades = products.Text, PrivateNotes = notes.Text };
                await Save(booking with { Appointments = [.. booking.Appointments.Where(a => a.Id != saved.Id), saved] });
            }), status);
        return panel;
    }
}
