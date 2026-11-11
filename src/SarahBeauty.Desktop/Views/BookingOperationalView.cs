using Microsoft.UI.Xaml;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class BookingOperationalView
{
    internal static UIElement Build(BookingReportFacts facts)
    {
        var panel=Form.Panel();
        panel.Add(Form.Title("Current booking facts at capture"),ReportTable.Build(["Current fact","Value"],
            [["Booking",facts.Name],["Booking identity",facts.BookingId],["Register revision",facts.Revision.ToString()],["Status",facts.Status],["Archived",facts.Archived?"Yes":"No"],
            ["Primary customer",facts.CurrentPrimaryCustomerName],["Event date",facts.Event.Date?.ToString("yyyy-MM-dd")??"Undecided"],["Venue",Text(facts.Event.Venue)],
            ["Ready-by local time",Text(facts.Event.ReadyBy)],["Event zone",facts.Event.TimeZoneId],["Ready-by UTC offset",Text(facts.Event.ReadyByUtcOffset)],["Trial included",facts.HasTrial?"Yes":"No"]]),
            Form.Text("These operational facts describe the records when this report was captured. Issued invoice/credit lines below retain their original values."),Charges(facts),Places(facts),Appointments(facts));
        return panel;
    }

    private static UIElement Charges(BookingReportFacts facts)=>PagedReport.Build("Current booking service charges",
        ["Description","Unit / quantity","Unit price","Discount","Net charge","Tax rate","Copied duration","Active"],facts.Charges.Select(c=>new[]{c.Description,
            c.Unit+" • "+c.Quantity.ToString("G29"),Money.Display(c.UnitCents),Money.Display(c.DiscountCents),Money.Display(c.NetCents),c.TaxRate?.ToString("P2")??"Unknown",
            c.DurationMinutes.HasValue?c.DurationMinutes.Value+" minutes":"Unknown",c.Active?"Yes":"No"}));

    private static UIElement Places(BookingReportFacts facts)=>PagedReport.Build("Current booking recipient places",
        ["Recipient","Charge","Place number","Active","Place identity"],facts.Places.Select(p=>new[]{p.CurrentName,
            facts.Charges.SingleOrDefault(c=>c.Id==p.ChargeId)?.Description??"Earlier charge",p.Ordinal.ToString(),p.Active?"Yes":"No",p.Id}));

    private static UIElement Appointments(BookingReportFacts facts)=>PagedReport.Build("Current booking appointments",
        ["Kind / identity","Artist","Start with offset","End with offset","Zone","Recipients","Status / location"],facts.Appointments.Select(a=>new[]{a.Kind+" • "+a.Id,a.CurrentArtistName,
            a.Start?.ToString("yyyy-MM-dd HH:mm zzz")??"Untimed",a.End?.ToString("yyyy-MM-dd HH:mm zzz")??"Untimed",Text(a.TimeZoneId),
            a.PlaceIds.Length==0?"Recipients not recorded":string.Join("; ",a.PlaceIds.Select(id=>facts.Places.SingleOrDefault(p=>p.Id==id)?.CurrentName??"Earlier place")),a.Status+" • "+Text(a.Location)}));

    private static string Text(string value)=>string.IsNullOrWhiteSpace(value)?"Undecided":value;
}
