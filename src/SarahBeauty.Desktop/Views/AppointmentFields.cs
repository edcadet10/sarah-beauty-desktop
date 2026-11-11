using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class AppointmentFields
{
    private readonly Appointment _original;
    private readonly Func<Booking> _booking;
    private readonly ComboBox _kind,_artist,_state;
    private readonly TextBox _start,_end,_zone,_location,_reason,_products,_notes;
    private readonly AppointmentRecipientFields _recipients;

    internal AppointmentFields(Booking booking,Appointment appointment,List<Artist> artists,List<Customer> customers,Func<Booking> currentBooking)
    {
        _original=appointment;_booking=currentBooking;_recipients=new(booking,appointment,customers);
        _kind=Form.Select("Appointment kind",["Trial","Wedding","Other"],appointment.Kind);
        _artist=Form.Records("Artist",artists.Where(a=>!a.Archived || a.Id==appointment.ArtistId).Select(a=>(a.Id,a.Name+(a.Archived?" • Archived":""))),appointment.ArtistId);
        _state=Form.Select("Appointment status",["Planned","Completed","Cancelled"],appointment.Status);
        _zone=Form.Input("Appointment time zone (blank means unconfirmed)",appointment.TimeZoneId);
        _start=Form.Input("Start (YYYY-MM-DDTHH:MM with UTC offset)",appointment.Start?.ToString("yyyy-MM-dd'T'HH:mmzzz")??"");
        _end=Form.Input("End (include the correct UTC offset)",appointment.End?.ToString("yyyy-MM-dd'T'HH:mmzzz")??"");
        _location=Form.Input("Appointment location",appointment.Location);_reason=Form.Input("Schedule override reason (when needed)",appointment.OverrideReason);
        _products=Form.Input("Products and shades used (private)",appointment.ProductsAndShades);_notes=Form.Input("Appointment notes (private)",appointment.PrivateNotes);
    }

    internal UIElement Build()
    {
        var panel=Form.Panel();var status=Form.Text();
        panel.Add(_kind,_artist,_recipients.Build(),_zone,_start,_end,status,Form.Action("Calculate end from known service durations",status,()=>
            {
                var end=AppointmentPlanning.EndFromDurations(_booking(),Read(includeEnd:false));_end.Text=end.ToString("yyyy-MM-dd'T'HH:mmzzz");
                status.Text="Proposed end calculated. Review it before saving; no appointment was saved by this calculation.";return Task.CompletedTask;
            }),_location,_state,_reason,_products,_notes,
            Form.Text("Use the actual UTC offset for the stated local time. Repeated clock-change times require the intended offset; unknown zones stay unconfirmed. Calculating an end does not confirm that work occurred."));
        return panel;
    }

    internal Appointment Read(bool includeEnd=true)=>_original with{Kind=(string)_kind.SelectedItem,ArtistId=_artist.SelectedId(),Status=(string)_state.SelectedItem,Start=Input.Time(_start.Text),End=includeEnd?Input.Time(_end.Text):null,
        TimeZoneId=_zone.Text.Trim(),Location=_location.Text.Trim(),OverrideReason=_reason.Text.Trim(),ProductsAndShades=_products.Text,PrivateNotes=_notes.Text,PlaceIds=_recipients.Read()};
}
