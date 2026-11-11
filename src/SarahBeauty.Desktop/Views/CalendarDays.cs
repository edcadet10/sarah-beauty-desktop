using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;

namespace SarahBeauty_Desktop.Views;

internal sealed class CalendarDays(CalendarSnapshot snapshot)
{
    private readonly StackPanel _days=Form.Panel();
    private readonly StackPanel _agenda=Form.Panel();
    private int _offset;

    internal UIElement Build()
    {
        var panel=Form.Panel();var status=Form.Text();
        panel.Add(_days,Form.Action("Earlier calendar dates",status,()=>{_offset=Math.Max(0,_offset-42);Draw();return Task.CompletedTask;}),
            Form.Action("Later calendar dates",status,()=>
            {
                if(snapshot.From.DayNumber+_offset+42<=snapshot.Through.DayNumber)_offset+=42;
                Draw();return Task.CompletedTask;
            }),status,_agenda);
        Draw();return panel;
    }

    private void Draw()
    {
        _days.Children.Clear();var first=snapshot.From.AddDays(_offset);
        int count=Math.Min(42,snapshot.Through.DayNumber-first.DayNumber+1),columns=count==1?1:7;
        var grid=new Grid{ColumnSpacing=8,RowSpacing=8,MinWidth=columns*86};
        for(int i=0;i<columns;i++)grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        int lead=columns==1?0:((int)first.DayOfWeek+6)%7;
        for(int row=0;row<(lead+count+columns-1)/columns;row++)grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
        for(int i=0;i<count;i++)AddDay(grid,first.AddDays(i),(lead+i)/columns,(lead+i)%columns);
        _days.Add(Form.Text($"Calendar dates {first:yyyy-MM-dd} through {first.AddDays(count-1):yyyy-MM-dd} • Select a date to review its appointments."),
            new ScrollViewer{HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollMode=ScrollMode.Enabled,Content=grid});
        Select(first);
    }

    private void AddDay(Grid grid,DateOnly day,int row,int column)
    {
        var slots=On(day);int issues=slots.Count(s=>s.Issues.Length>0);
        var button=new Button{Content=$"{day:ddd yyyy-MM-dd}\n{slots.Length} appointment(s)\n{issues} with review issues",MinHeight=76,HorizontalAlignment=HorizontalAlignment.Stretch};
        button.Click+=(_,_)=>Select(day);Grid.SetRow(button,row);Grid.SetColumn(button,column);grid.Children.Add(button);
    }

    private CalendarSlot[] On(DateOnly day)
    {
        var zone=TimeZoneInfo.FindSystemTimeZoneById(snapshot.TimeZoneId);
        return snapshot.Scheduled.Where(s=>DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(s.Start!.Value,zone).DateTime)<=day
            && DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(s.End!.Value.AddTicks(-1),zone).DateTime)>=day).ToArray();
    }

    private void Select(DateOnly day)
    {
        _agenda.Children.Clear();_agenda.Add(Form.Title($"Appointments on {day:yyyy-MM-dd}"),CalendarAgenda.Build(On(day),snapshot.TimeZoneId));
    }
}
