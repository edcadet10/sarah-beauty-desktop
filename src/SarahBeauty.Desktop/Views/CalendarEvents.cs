using Microsoft.UI.Xaml;
using SarahBeauty.Business.Bookings;

namespace SarahBeauty_Desktop.Views;

internal static class CalendarEvents
{
    internal static UIElement Build(CalendarBooking[] events)
    {
        var panel=Form.Panel();var table=Form.Panel();var count=Form.Text();var status=Form.Text();int page=0;
        void Show()
        {
            table.Children.Clear();count.Text=$"{events.Length} event(s) • Page {page+1} of {Math.Max(1,(events.Length+99)/100)}";
            table.Add(ReportTable.Build(["Booking / status","Event-local date / zone","Venue / ready by","Event work scheduled"],events.Skip(page*100).Take(100).Select(b=>new[]{
                $"{b.Name} • {b.Status}\n{b.Id}",$"{b.EventDate?.ToString("yyyy-MM-dd")??"Undecided"}\n{b.TimeZoneId}",
                $"{(b.Venue.Length==0?"Venue undecided":b.Venue)}\n{(b.ReadyBy.Length==0?"Ready-by undecided":b.ReadyBy)}",b.Status=="Cancelled"?"Cancelled":b.EventWorkScheduled?"Yes":"Needs review"})));
        }
        panel.Add(count,table,Form.Action("Earlier events",status,()=>{page=Math.Max(0,page-1);Show();return Task.CompletedTask;}),
            Form.Action("Later events",status,()=>{if((page+1)*100<events.Length)page++;Show();return Task.CompletedTask;}),status);
        Show();return panel;
    }
}
