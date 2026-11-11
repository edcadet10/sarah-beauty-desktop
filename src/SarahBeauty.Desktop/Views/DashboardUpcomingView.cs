using Microsoft.UI.Xaml;
using SarahBeauty.Business.Dashboard;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class DashboardUpcomingView
{
    internal static UIElement Build(DashboardUpcomingSnapshot snapshot,Action<string> navigate)
    {
        var panel=Form.Panel();var status=Form.Text();var bookings=snapshot.Bookings.Concat(snapshot.Undated).ToArray();
        panel.Add(Form.Title("Upcoming work sources"),Form.Text($"Planning date {snapshot.PlanningAsOf:yyyy-MM-dd} • Event window {snapshot.From:yyyy-MM-dd} through {snapshot.Through:yyyy-MM-dd}.\n"
            +"Confirmed status and artist assignments are current at capture. Agreement value includes tax and is separate from income/cash. Values needing review are excluded from the known subtotal."),
            Rows("Dated confirmed weddings",snapshot.Bookings),Rows("Undated confirmed weddings",snapshot.Undated),
            PagedReport.Build("Upcoming payer milestones",["Booking","Invoice / version","Payer","Milestone","Due","Remaining including tax"],
                bookings.SelectMany(b=>b.Billing.Dues.Where(d=>d.Cents>0).Select(d=>new[]{b.Name,d.Number+" • "+d.InvoiceVersionId,d.CustomerName,d.Kind,d.DueDate?.ToString("yyyy-MM-dd")??"Unknown",Money.Display(d.Cents)}))),
            status,Form.Action("Open booking records",status,()=>{navigate("Bookings");return Task.CompletedTask;}),
            Form.Action("Open work calendar",status,()=>{navigate("Calendar");return Task.CompletedTask;}));return panel;
    }

    private static UIElement Rows(string title,IEnumerable<DashboardUpcomingBooking> bookings)=>PagedReport.Build(title,
        ["Booking / ID","Event date","Venue / ready by / zone","Artists at capture","Accepted agreement / version / hash","Known value including tax","Reviews"],
        bookings.Select(b=>new[]{b.Name+" • "+b.BookingId,b.EventDate?.ToString("yyyy-MM-dd")??"Undecided",b.Venue+" • "+b.ReadyBy+" • "+b.TimeZoneId,
            string.Join("; ",b.Artists.Select(a=>a.Name+" • "+a.State+" • "+a.Id)),b.QuoteLabel+" • "+b.QuoteVersionId+" • "+b.QuoteHash,
            b.AgreementGrossCents.HasValue?Money.Display(b.AgreementGrossCents.Value):"Needs review",b.ReviewIssues.Length==0?"No recorded blockers":string.Join("; ",b.ReviewIssues)}));
}
