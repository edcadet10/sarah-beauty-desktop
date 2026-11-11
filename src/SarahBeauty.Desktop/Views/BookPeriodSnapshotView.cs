using Microsoft.UI.Xaml;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class BookPeriodSnapshotView
{
    internal static UIElement Build(BookPeriodPreview preview,IReadOnlyDictionary<string,string> owners)
    {
        var r=preview.Reports;var panel=Form.Panel();
        panel.Add(Form.Title($"Book period {r.From:yyyy-MM-dd} – {r.Through:yyyy-MM-dd}"),
            Form.Text($"{r.BusinessName} • {r.Mode} • Management cash-method policy • Snapshot version {preview.Version}\nRecord version {r.Watermark} • Generated {r.GeneratedAtUtc}"),
            Form.Text("Closing reason: "+preview.Draft.Reason),Form.Text("Closing evidence: "+preview.Draft.EvidenceId),
            ReportTable.Build(["Period measure","USD"],[["Income",Money.Display(r.ProfitLoss.IncomeCents)],["Expenses",Money.Display(r.ProfitLoss.ExpenseCents)],
                ["Result",Money.Display(r.ProfitLoss.ResultCents)],["Assets",Money.Display(r.BalanceSheet.AssetsCents)],["Liabilities",Money.Display(r.BalanceSheet.LiabilitiesCents)],["Equity including result",Money.Display(r.BalanceSheet.EquityCents)]]),
            Form.Text("Previous closed-through date: "+(preview.PreviouslyClosedThrough?.ToString("yyyy-MM-dd")??"None")));
        foreach(string issue in preview.BlockingIssues)panel.Add(Form.Text(issue));
        panel.Add(CapturedReports(r),
            PagedReport.Build("Period-end reconciliations",["Account code","As-of date","Reconciliation code","Owner / time"],
                preview.Reconciliations.Select(c=>new[]{r.TrialBalance.Single(a=>a.Account.Id==c.AccountId).Account.ToString(),c.AsOfDate.ToString("yyyy-MM-dd"),c.Id[..8],owners.GetValueOrDefault(c.ApprovedBy,"Former owner")+" • "+c.AtUtc})));
        return panel;
    }

    private static UIElement CapturedReports(ReportSnapshot report)
    {
        var panel=Form.Panel();var result=Form.Panel();
        var kind=Form.Select("Captured financial report",["Profit and loss","Balance sheet","Trial balance","Cash movements","Customer aging","Supplier balances","Tax activity","Asset schedule","Debt schedule","Owner schedule","Booking contribution"],"Profit and loss");
        void Show(){result.Children.Clear();result.Add(ReportsView.Table(report,kind.SelectedItem as string));}
        kind.SelectionChanged+=(_,_)=>Show();
        panel.Add(Form.Text("These report tables use the captured records. Switching reports does not load newer business changes."),kind,result,
            PagedReport.Build("Captured posted entries",["Date / movement","Account","Debit","Credit"],report.Entries.Select(e=>new[]{e.Date.ToString("yyyy-MM-dd")+" • "+e.Kind+" • "+e.Memo,e.AccountName,Money.Display(e.DebitCents),Money.Display(e.CreditCents)})),
            PagedReport.Build("Captured supplier bills",["Bill","Purchase date","Due date","Unpaid","Supplier credit"],report.SupplierBills.Select(b=>new[]{b.Name,b.PurchaseDate.ToString("yyyy-MM-dd"),b.DueDate?.ToString("yyyy-MM-dd")??"Unknown",Money.Display(b.PayableCents),Money.Display(b.CreditCents)})));
        Show();return new Microsoft.UI.Xaml.Controls.Expander{Header="Captured financial reports",Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch};
    }
}
