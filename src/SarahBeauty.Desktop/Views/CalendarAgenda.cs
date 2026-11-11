using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;

namespace SarahBeauty_Desktop.Views;

internal static class CalendarAgenda
{
    internal static UIElement Build(CalendarSlot[] slots,string zone)
    {
        var panel=Form.Panel();var detail=Form.Panel();var status=Form.Text();var count=Form.Text();
        var list=new ListView{MaxHeight=350,SelectionMode=ListViewSelectionMode.Single};int page=0;
        void Show()
        {
            list.ItemsSource=slots.Skip(page*100).Take(100).Select(s=>new Entry(s)).ToArray();detail.Children.Clear();
            count.Text=slots.Length==0?"No appointments in this selection.":$"{slots.Length} appointment(s) • Page {page+1} of {(slots.Length+99)/100} • Select an appointment for source details.";
        }
        list.SelectionChanged+=(_,_)=>{detail.Children.Clear();if(list.SelectedItem is Entry entry)detail.Add(Details(entry.Slot,zone));};
        panel.Add(count,list,Form.Action("Earlier appointments",status,()=>{page=Math.Max(0,page-1);Show();return Task.CompletedTask;}),
            Form.Action("Later appointments",status,()=>{if((page+1)*100<slots.Length)page++;Show();return Task.CompletedTask;}),status,detail);
        Show();return panel;
    }

    private static UIElement Details(CalendarSlot slot,string zone)
    {
        var panel=Form.Panel();
        panel.Add(Form.Title("Appointment source"),Form.Text($"{slot.BookingName} • Booking {slot.BookingId}\n{slot.Kind} • {slot.Status} • Booking status {slot.BookingStatus}\nAppointment {slot.AppointmentId}\nArtist: {slot.ArtistName}\nLocation: {(slot.Location.Length==0?"Undecided":slot.Location)}"),
            Form.Text($"Display zone {zone}\nStart: {Time(slot.Start)}\nEnd: {Time(slot.End)}\nStated zone: {(slot.StatedTimeZoneId.Length==0?"Unconfirmed":slot.StatedTimeZoneId)}\nStated start: {Time(slot.StatedStart)}\nStated end: {Time(slot.StatedEnd)}"),
            Form.Text(slot.Recipients.Length==0?"Recipient places: Undecided":string.Join("\n",slot.Recipients.Select(r=>$"{r.Service} • place {r.Ordinal} • {r.Name} • {r.PlaceId}"+(r.Active?"":" • Retired")))),
            Form.Text(slot.OverrideReason.Length==0?"No schedule override reason recorded.":"Recorded schedule override: "+slot.OverrideReason));
        foreach(var issue in slot.Issues)panel.Add(Form.Text($"{issue.Code}: {issue.Message}"+(issue.OverrideRecorded?" Recorded override applies.":"")
            +(issue.RelatedAppointmentId.Length==0?"":$" Related booking {issue.RelatedBookingId}; appointment {issue.RelatedAppointmentId}.")));
        return panel;
    }

    private static string Time(DateTimeOffset? time)=>time?.ToString("yyyy-MM-dd HH:mm zzz")??"Undecided";

    private sealed record Entry(CalendarSlot Slot)
    {
        public override string ToString()=>$"{Time(Slot.Start)} → {Time(Slot.End)} • {Slot.BookingName} • {Slot.Kind} • {Slot.ArtistName} • {Slot.Status}"
            +(Slot.Cancelled?" • Cancelled":"")+$" • {Slot.Issues.Length} review issue(s) • {Slot.AppointmentId[..8]}";
    }
}
