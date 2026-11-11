using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Dashboard;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class DashboardChartsView
{
    internal static UIElement Build(DashboardSnapshot snapshot)
    {
        var panel=Form.Panel();var f=snapshot.Finance;var r=f.Reports;var months=f.Months.Select(m=>m.Month.ToString("yyyy-MM")).ToArray();
        panel.Add(Form.Title("Dashboard charts"),Form.Text($"Financial charts: {r.From:yyyy-MM-dd} through {r.Through:yyyy-MM-dd} • USD • Approved cash-method classifications.\n"
            +"Scroll wide charts horizontally. Exact accessible tables accompany every chart; the plot scale is approximate. Missing monthly context stays a gap, including in the result line."),
            Section("Monthly income and expenses",ColumnChart.Build(months,[new("Income",f.Months.Select(m=>m.IncomeCents).ToArray(),ChartDrawing.Blue),new("Expenses",f.Months.Select(m=>m.ExpenseCents).ToArray(),ChartDrawing.Orange)]),Monthly(f,"Exact income and expense figures")),
            Section("Monthly result",LineChart.Build(months,f.Months.Select(m=>m.ResultCents).ToArray()),Monthly(f,"Exact result figures")),
            Section("Customer aging chart",BarChart.Build(f.Aging.Select(a=>a.Name).ToArray(),f.Aging.Select(a=>a.Cents).ToArray(),ChartDrawing.Blue),
                ReportTable.Build(["Band as of "+r.Through.ToString("yyyy-MM-dd"),"Open including tax","Milestone shares"],f.Aging.Select(a=>new[]{a.Name,Money.Display(a.Cents),a.Sources.Length.ToString()}))),
            Upcoming(snapshot),Expenses(f),Owners(f));return panel;
    }

    private static UIElement Section(string title,UIElement chart,UIElement table,string note="")
    {
        var panel=Form.Panel();if(note.Length>0)panel.Add(Form.Text(note));panel.Add(chart,table);
        return new Expander{Header=title,Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch};
    }

    private static UIElement Monthly(DashboardFinanceSnapshot snapshot,string title)=>PagedReport.Build(title,
        ["Month","Income","Expenses","Result","Posted activity","Closed coverage"],snapshot.Months.Select(m=>new[]{m.Month.ToString("yyyy-MM"),
            Value(m.IncomeCents),Value(m.ExpenseCents),Value(m.ResultCents),m.HasPostedActivity?"Yes":"No",m.HasClosedCoverage?"Yes":"No"}));
    private static string Value(long? value)=>value.HasValue?Money.Display(value.Value):"No confirmed activity context";

    private static UIElement Upcoming(DashboardSnapshot snapshot)
    {
        var upcoming=snapshot.Upcoming;var months=upcoming.Months;
        return Section("Upcoming confirmed weddings chart",ColumnChart.Build(months.Select(m=>m.Month.ToString("yyyy-MM")+$"\n{m.Count} events\n{m.UnknownValueCount} need review").ToArray(),
            [new("Known agreement value including tax",months.Select(m=>(long?)m.KnownAgreementGrossCents).ToArray(),ChartDrawing.Purple)]),
            PagedReport.Build("Exact upcoming monthly figures",["Event month","Confirmed count","Known value including tax","Values needing review"],
                months.Select(m=>new[]{m.Month.ToString("yyyy-MM"),m.Count.ToString(),Money.Display(m.KnownAgreementGrossCents),m.UnknownValueCount.ToString()})),
            $"Planning date {upcoming.PlanningAsOf:yyyy-MM-dd} • Event window {upcoming.From:yyyy-MM-dd} through {upcoming.Through:yyyy-MM-dd}. Counts include values needing review; the known subtotal excludes them. {upcoming.Undated.Length} undated confirmed bookings stay separate. Commitments are not cash or income.");
    }

    private static UIElement Expenses(DashboardFinanceSnapshot snapshot)
    {
        var expenses=snapshot.Expenses.Where(e=>e.Cents!=0).ToArray();return Section("Expense categories chart",
            BarChart.Build(expenses.Select(e=>e.Code+" • "+e.Name).ToArray(),expenses.Select(e=>e.Cents).ToArray(),ChartDrawing.Orange),
            PagedReport.Build("Exact expense categories",["Account","Net expense"],expenses.Select(e=>new[]{e.Code+" • "+e.Name,Money.Display(e.Cents)})),
            "Includes approved depreciation and dated corrections/refunds. Transfers, owner draws, and loan principal are excluded. Negative net categories remain visible.");
    }

    private static UIElement Owners(DashboardFinanceSnapshot snapshot)
    {
        var owners=snapshot.Reports.Owners?.Positions.ToArray()??[];return Section("Owner movements chart",ColumnChart.Build(owners.Select(o=>o.Name).ToArray(),
            [new("Contributions",owners.Select(o=>(long?)o.ContributionsCents).ToArray(),ChartDrawing.Blue),new("Draws",owners.Select(o=>(long?)o.DrawsCents).ToArray(),ChartDrawing.Orange),
            new("Owner-loan proceeds",owners.Select(o=>(long?)o.LoanProceedsCents).ToArray(),ChartDrawing.Purple),new("Owner-loan principal paid",owners.Select(o=>(long?)o.LoanPrincipalPaidCents).ToArray(),ChartDrawing.Green)]),
            PagedReport.Build("Exact owner movements",["Owner","Contributions","Draws","Loan proceeds","Principal paid","Confirmed attributed principal as of end"],
                owners.Select(o=>new[]{o.Name,Money.Display(o.ContributionsCents),Money.Display(o.DrawsCents),Money.Display(o.LoanProceedsCents),Money.Display(o.LoanPrincipalPaidCents),Money.Display(o.AttributedLoanPrincipalCents)})),
            "Separate actual transaction categories; ownership percentages do not divide them. Loan attribution uses captured lender facts; unconfirmed or external lender balances remain in the debt/owner source schedules.");
    }
}
