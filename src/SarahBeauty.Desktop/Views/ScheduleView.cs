using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class ScheduleView(BusinessStore store,OwnerSession owner)
{
    private readonly ComboBox _view=Form.Select("Calendar view",["Day","Week","Month","Date range"],"Week");
    private readonly TextBox _from=Form.Input("Calendar start (YYYY-MM-DD)",DateTime.Today.ToString("yyyy-MM-dd"));
    private readonly TextBox _through=Form.Input("Calendar end (date range only)",DateTime.Today.ToString("yyyy-MM-dd"));
    private readonly TextBox _zone=Form.Input("Calendar display time zone","America/New_York");
    private readonly CheckBox _cancelled=new(){Content="Include cancelled work",MinHeight=44};
    private readonly TextBlock _status=Form.Text();
    private readonly StackPanel _result=Form.Panel();

    internal UIElement Build()
    {
        var panel=Form.Panel();
        var artists=Form.Records("Calendar artist",new BookingBook(store).Artists(owner).Select(a=>(a.Id,a.Name+(a.Archived?" • Archived":""))),noneLabel:"All artists");
        panel.Add(Form.Title("Calendar"),Form.Text("Review appointments in a stated display zone. Week starts on Monday; month uses the start date's month. Changing filters requires Load calendar."),
            _view,_from,_through,_zone,artists,_cancelled,_status,Form.Action("Load calendar",_status,async()=>
            {
                var (first,last)=Window();string zone=_zone.Text.Trim(),artist=artists.SelectedId(),artistLabel=artists.SelectedItem?.ToString()??"All artists";bool cancelled=_cancelled.IsChecked==true;
                var snapshot=await Task.Run(()=>new CalendarSources(store).Load(owner,first,last,zone,artist,cancelled));
                Show(snapshot,artistLabel,cancelled);
            }),_result);
        return panel;
    }

    private (DateOnly First,DateOnly Last) Window()
    {
        var first=Input.Date(_from.Text)??throw new ArgumentException("Enter the calendar start date.");
        return (_view.SelectedItem as string) switch
        {
            "Day"=>(first,first),
            "Week"=>Week(first),
            "Month"=>(new(first.Year,first.Month,1),new(first.Year,first.Month,DateTime.DaysInMonth(first.Year,first.Month))),
            _=>(first,Input.Date(_through.Text)??throw new ArgumentException("Enter the calendar end date."))
        };
    }

    private static (DateOnly,DateOnly) Week(DateOnly date)
    {
        var monday=date.AddDays(-((int)date.DayOfWeek+6)%7);
        return (monday,monday.AddDays(6));
    }

    private void Show(CalendarSnapshot snapshot,string artist,bool cancelled)
    {
        _result.Children.Clear();
        _result.Add(Form.Title("Captured calendar"),Form.Text($"{snapshot.BusinessName} • {snapshot.Mode} • Watermark {snapshot.Watermark}\n"
            +$"{snapshot.From:yyyy-MM-dd} through {snapshot.Through:yyyy-MM-dd} • Display zone {snapshot.TimeZoneId}\nArtist: {artist} • Cancelled work {(cancelled?"included":"excluded")}\nGenerated {snapshot.GeneratedAtUtc}"),
            new CalendarDays(snapshot).Build(),Section("All scheduled appointments in this window",CalendarAgenda.Build(snapshot.Scheduled,snapshot.TimeZoneId)),
            Section("Untimed appointments • all dates in this artist filter",CalendarAgenda.Build(snapshot.Unscheduled,snapshot.TimeZoneId)),
            Section("Event dates • each event's recorded local date",CalendarEvents.Build(snapshot.Events)),
            Section("Event date undecided",CalendarEvents.Build(snapshot.DateUndecided)));
    }

    private static Expander Section(string name,UIElement content)=>new(){Header=name,Content=content,HorizontalAlignment=HorizontalAlignment.Stretch};
}
