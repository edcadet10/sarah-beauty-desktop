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
            var edited=_fields?.Read(booking)??booking;
            await Task.Run(() => _book.SaveArtist(_owner, new Artist { Name = artistName }));
            Edit(edited);
        }), status);
        foreach (var appointment in booking.Appointments)
            panel.Add(Section($"{appointment.Kind} • {appointment.Start?.ToString("yyyy-MM-dd HH:mm zzz") ?? "Time undecided"} • {(appointment.TimeZoneId.Length>0?appointment.TimeZoneId:"Zone unconfirmed")} • {(appointment.PlaceIds.Length>0?appointment.PlaceIds.Length+" recipient place(s)":"Recipients undecided")}", AppointmentEditor(booking, appointment)));
        panel.Add(Section("New appointment", AppointmentEditor(booking,
            new Appointment(Guid.NewGuid().ToString(), booking.HasTrial ? "Trial" : "Other", "", null, null, ""){TimeZoneId=booking.EventTimeZoneId})));
        return panel;
    }

    private UIElement AppointmentEditor(Booking booking, Appointment appointment)
    {
        var panel = Form.Panel();
        var fields=new AppointmentFields(booking,appointment,_book.Artists(_owner),_customers,()=>_fields?.Read(booking)??booking);
        var status = Form.Text();
        panel.Add(fields.Build(),
            Form.Action("Save appointment", status, async () =>
            {
                var saved=fields.Read();
                await Save(booking with { Appointments = [.. booking.Appointments.Where(a => a.Id != saved.Id), saved] });
            }), status);
        return panel;
    }
}
