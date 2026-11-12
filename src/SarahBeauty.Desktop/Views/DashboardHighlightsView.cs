using Microsoft.UI.Xaml;
using SarahBeauty.Business.Dashboard;

namespace SarahBeauty_Desktop.Views;

internal static class DashboardHighlightsView
{
    internal static UIElement Build(DashboardSnapshot snapshot,Action reviewWork,Action reviewBookings)=>ResponsiveCards.Build([
        Card("Work to review",$"{snapshot.Operations.Work.Length} items",$"Planning date {snapshot.Operations.BusinessDate:yyyy-MM-dd}","Review work and supply alerts",reviewWork),
        Card("Upcoming confirmed weddings",$"{snapshot.Upcoming.Bookings.Length} dated • {snapshot.Upcoming.Undated.Length} undated",
            $"{snapshot.Upcoming.From:yyyy-MM-dd} through {snapshot.Upcoming.Through:yyyy-MM-dd}","Review upcoming bookings",reviewBookings)
    ],2);

    private static UIElement Card(string title,string value,string detail,string action,Action open)
    {
        var panel=Form.Panel();var status=Form.Text();
        panel.Add(Form.Text(title),Form.Title(value),Form.Muted(detail),Form.Action(action,status,()=>{open();return Task.CompletedTask;}),status);
        return Form.Card(panel);
    }
}
