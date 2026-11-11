using Microsoft.UI.Xaml;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class BookingReportView
{
    internal static UIElement Build(ReportSnapshot report)
    {
        if(report.Bookings is null)return Form.Text("The booking report was not captured in this report version.");
        if(report.Bookings.Version!=1)return Form.Text("This booking report version requires a compatible app version.");
        var snapshot=report.Bookings;var panel=Form.Panel();var known=snapshot.Positions.Where(p=>p.ContributionEstimateCents.HasValue).ToArray();
        panel.Add(Form.Text($"Booking report version {snapshot.Version} • As of {report.Through:yyyy-MM-dd}\nKnown contribution estimate subtotal: {Money.Display(known.Sum(p=>p.ContributionEstimateCents!.Value))} • Bookings without issued charges: {snapshot.Positions.Count-known.Length}"),
            Form.Text("Estimate = issued net charges after linked credit notes, excluding sales tax, less recorded direct costs. It does not establish exact profit or unrecorded product consumption. P&L uses recognized cash-method income."),
            Charges(snapshot),Period(snapshot,report.From),
            Form.Text($"Unassigned recorded direct costs: {Money.Display(snapshot.UnassignedDirectCosts.Sum(e=>e.DebitCents-e.CreditCents))} through the as-of date; "
                +$"{Money.Display(snapshot.UnassignedDirectCosts.Where(e=>e.Date>=report.From).Sum(e=>e.DebitCents-e.CreditCents))} within the selected period. These costs are not divided between bookings."),
            BookingFinancialView.Entries("Unassigned direct cost sources",snapshot.UnassignedDirectCosts,report.From));
        AddDetails(panel,snapshot);return panel;
    }

    private static UIElement Charges(BookingReportSnapshot snapshot)=>PagedReport.Build("Booking issued-charge estimates",
        ["Booking","Effective net charges","Recorded direct costs","Contribution estimate","Applied incl. tax","Due incl. tax"],
        snapshot.Positions.Select(p=>new[]{Caption(p),Money.Display(p.EffectiveNetChargesCents),Money.Display(p.Financial.RecordedDirectCostCents),
            p.ContributionEstimateCents.HasValue?Money.Display(p.ContributionEstimateCents.Value):"No issued charges",Money.Display(p.AppliedCents),Money.Display(p.OpenCents)}));

    private static UIElement Period(BookingReportSnapshot snapshot,DateOnly from)=>PagedReport.Build("Booking period activity",
        ["Booking","Net charge change","Recorded direct costs","Recognized service income","Receipt/refund gross net","Receipt fees net","Source net cash"],
        snapshot.Positions.Select(p=>new[]{Caption(p),Money.Display(p.PeriodNetChargesCents),Money.Display(p.Financial.PeriodRecordedDirectCostCents),Money.Display(p.Financial.PeriodRecognizedServiceIncomeCents),
            Money.Display(p.Financial.Cash.Where(c=>c.Date>=from).Sum(c=>c.SignedGrossCents)),Money.Display(p.Financial.Cash.Where(c=>c.Date>=from).Sum(c=>c.SignedFeeCents)),
            Money.Display(p.Financial.Cash.Where(c=>c.Date>=from).Sum(c=>c.SignedNetCashCents))}));

    private static void AddDetails(Microsoft.UI.Xaml.Controls.StackPanel panel,BookingReportSnapshot snapshot)
    {
        var booking=Form.Records("Supporting booking",snapshot.Positions.Select(p=>(p.CurrentFacts.BookingId,Caption(p))));var detail=Form.Panel();var status=Form.Text();
        panel.Add(booking,status,Form.Action("Show captured booking facts and sources",status,()=>
        {
            var position=snapshot.Positions.SingleOrDefault(p=>p.CurrentFacts.BookingId==booking.SelectedId())??throw new ArgumentException("Choose a booking from this captured report.");
            detail.Children.Clear();detail.Add(BookingOperationalView.Build(position.CurrentFacts),BookingBillingView.Build(position.Billing),BookingFinancialView.Build(position.Financial));return Task.CompletedTask;
        }),detail);
    }

    private static string Caption(BookingReportPosition p)=>p.CurrentFacts.Name+" • "+p.CurrentFacts.BookingId[..8];
}
