using Microsoft.UI.Xaml;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class TaxFilingView
{
    internal static UIElement Build(ReportSnapshot report)
    {
        if(report.TaxFilings is null)return Form.Text("Filing reviews were not captured in this report version.");
        if(report.TaxFilings.Version!=1)return Form.Text("This filing review version requires a compatible app version.");
        var snapshot=report.TaxFilings;var panel=Form.Panel();
        panel.Add(Form.Title("Current filing reviews at capture"),Form.Text("Registered tax periods below overlap the selected financial period. Filing status describes the records at capture time; money uses its separately stated transaction/as-of dates. A remittance does not file a return."),
            PagedReport.Build("Captured tax filing periods",["Period / jurisdiction","Reporting dates","Due date","Status at capture","Latest filed/amended date","Reported tax","Archived"],snapshot.Periods.Select(Row)));
        if(snapshot.Periods.Count==0)panel.Add(Form.Text("No registered tax periods overlap this financial period."));
        AddHistory(panel,snapshot);return panel;
    }

    private static string[] Row(TaxFilingPeriodSnapshot period)
    {
        var latest=period.History.LastOrDefault(h=>h.Decision.Status is "Filed" or "Amended");
        return [period.Name+" • "+period.Jurisdiction,$"{period.From:yyyy-MM-dd} – {period.Through:yyyy-MM-dd}",period.DueDate?.ToString("yyyy-MM-dd")??"Unknown",period.StatusAtCapture,
            latest?.Decision.FilingDate?.ToString("yyyy-MM-dd")??"Not recorded",Amount(latest?.Decision.ReportedTaxCents),period.Archived?"Yes":"No"];
    }

    private static void AddHistory(Microsoft.UI.Xaml.Controls.StackPanel panel,TaxFilingSnapshot snapshot)
    {
        var period=Form.Records("Supporting captured tax period",snapshot.Periods.Select(p=>(p.Id,p.Name+" • "+p.Jurisdiction)));var detail=Form.Panel();var status=Form.Text();
        panel.Add(period,status,Form.Action("Show captured filing review history",status,()=>
        {
            var saved=snapshot.Periods.SingleOrDefault(p=>p.Id==period.SelectedId())??throw new ArgumentException("Choose a tax period from this captured report.");
            detail.Children.Clear();detail.Add(Form.Text($"Captured tax period identity: {saved.Id} • Register revision {saved.Revision}"),
                PagedReport.Build("Captured filing review history",["Decision","Filing date","Reported tax","Reference / evidence","Reason","Owner at capture / review time"],saved.History.Select(h=>new[]{h.Decision.Status,
                    h.Decision.FilingDate?.ToString("yyyy-MM-dd")??"Not recorded",Amount(h.Decision.ReportedTaxCents),h.Decision.Reference+" • "+h.Decision.EvidenceId,h.Decision.Reason,h.OwnerNameAtCapture+" • "+h.AtUtc})));
            return Task.CompletedTask;
        }),detail);
    }

    private static string Amount(long? cents)=>cents.HasValue?Money.Display(cents.Value):"Not recorded";
}
