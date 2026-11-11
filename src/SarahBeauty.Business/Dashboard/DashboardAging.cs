using SarahBeauty.Business.Finance;

namespace SarahBeauty.Business.Dashboard;

internal static class DashboardAging
{
    internal static DashboardAgingBand[] Read(ReportSnapshot reports)
    {
        var dues=reports.Bookings!.Positions.SelectMany(b=>b.Billing.Dues).Where(d=>d.Cents>0).ToArray();
        DashboardAgingBand Band(string name,Func<InvoiceDue,bool> accepts)
        {
            var sources=dues.Where(accepts).ToArray();return new(name,sources.Sum(d=>d.Cents),sources);
        }
        int Age(InvoiceDue due)=>reports.Through.DayNumber-due.DueDate!.Value.DayNumber;
        return [Band("Not due, including today",d=>d.DueDate>=reports.Through),
            Band("1–7 days overdue",d=>d.DueDate.HasValue && Age(d) is >=1 and <=7),
            Band("8–30 days overdue",d=>d.DueDate.HasValue && Age(d) is >=8 and <=30),
            Band("Over 30 days overdue",d=>d.DueDate.HasValue && Age(d)>30),Band("Due date unknown",d=>d.DueDate is null)];
    }
}
