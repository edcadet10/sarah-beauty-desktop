using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Dashboard;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class DashboardFinanceView
{
    internal static UIElement Build(DashboardFinanceSnapshot snapshot,Action<string> navigate)
    {
        var r=snapshot.Reports;var panel=Form.Panel();var status=Form.Text();
        panel.Add(Form.Title("Financial sources"),Form.Text($"Approved cash-method period {r.From:yyyy-MM-dd} through {r.Through:yyyy-MM-dd}; balances as of {r.Through:yyyy-MM-dd}."));
        if(r.OpeningWarnings.Length>0)panel.Add(Form.Text("Unconfirmed inputs: "+string.Join("; ",r.OpeningWarnings)));
        panel.Add(Cash(snapshot),PagedReport.Build("Dashboard customer balances",
            ["Payer","Open","Overdue","Due today","Future","Date unknown","Unallocated income receipts","Held receipts","Unused credits"],
            r.Customers.Select(c=>new[]{c.Name,Money.Display(c.OpenCents),Money.Display(c.OverdueCents),Money.Display(c.DueTodayCents),Money.Display(c.FutureCents),Money.Display(c.UnknownDueCents),Money.Display(c.UnallocatedReceiptsCents),Money.Display(c.HeldReceiptsCents),Money.Display(c.UnusedCreditCents)})),
            PagedReport.Build("Dashboard aging sources",["Band","Invoice / version","Payer","Milestone","Due","Remaining including tax"],
                snapshot.Aging.SelectMany(b=>b.Sources.Select(d=>new[]{b.Name,d.Number+" • "+d.InvoiceVersionId,d.CustomerName,d.Kind,d.DueDate?.ToString("yyyy-MM-dd")??"Unknown",Money.Display(d.Cents)}))),
            PagedReport.Build("Dashboard supplier balances",["Supplier","Open","Overdue","Due today","Future","Date unknown","Available credit"],
                r.Suppliers.Select(s=>new[]{s.Name,Money.Display(s.OpenCents),Money.Display(s.OverdueCents),Money.Display(s.DueTodayCents),Money.Display(s.FutureCents),Money.Display(s.UnknownDueCents),Money.Display(s.CreditCents)})),
            new Expander{Header="Dashboard debt sources",Content=DebtScheduleView.Build(r),HorizontalAlignment=HorizontalAlignment.Stretch},
            new Expander{Header="Dashboard tax sources",Content=ReportsView.Table(r,"Tax activity"),HorizontalAlignment=HorizontalAlignment.Stretch},
            new Expander{Header="Dashboard owner sources",Content=OwnerScheduleView.Build(r),HorizontalAlignment=HorizontalAlignment.Stretch},
            status,Form.Action("Open financial reports",status,()=>{navigate("Reports");return Task.CompletedTask;}),Form.Action("Open reconciliation",status,()=>{navigate("Reconciliation");return Task.CompletedTask;}));
        AddAccountEntries(panel,snapshot);return panel;
    }

    private static UIElement Cash(DashboardFinanceSnapshot snapshot)=>PagedReport.Build("Dashboard cash balances",
        ["Account","Kind","Active","Posted balance","Opening","Last reconciled through","Reconciliation ID / hash"],
        snapshot.CashAccounts.Select(c=>new[]{c.Account.ToString(),c.Account.CashKind,c.Account.Active?"Yes":"No",Money.Display(c.BalanceCents),
            c.Account.OpeningConfirmed?"Confirmed":"Unconfirmed",c.ReconciledThrough?.ToString("yyyy-MM-dd")??"Not completed",c.ReconciliationId+" • "+c.ReconciliationHash}));

    private static void AddAccountEntries(StackPanel panel,DashboardFinanceSnapshot snapshot)
    {
        var account=Form.Records("Dashboard supporting account",snapshot.Reports.TrialBalance.Select(a=>(a.Account.Id,a.Account.ToString())));var details=Form.Panel();var status=Form.Text();
        panel.Add(account,status,Form.Action("Show dashboard account entries",status,()=>
        {
            string id=account.SelectedId();var chosen=snapshot.Reports.TrialBalance.SingleOrDefault(a=>a.Account.Id==id)??throw new ArgumentException("Choose an account from this captured dashboard.");
            bool cash=chosen.Account.CashKind!="None";var entries=cash?snapshot.CashEntries.Where(e=>e.AccountId==id):snapshot.Reports.Entries.Where(e=>e.AccountId==id);
            details.Children.Clear();details.Add(Form.Text(cash?$"All captured cash entries through {snapshot.Reports.Through:yyyy-MM-dd}.":$"Captured period entries {snapshot.Reports.From:yyyy-MM-dd} through {snapshot.Reports.Through:yyyy-MM-dd}."),
                PagedReport.Build("Dashboard supporting entries",["Date","Kind / memo","Movement ID","Debit","Credit"],entries.Select(e=>new[]{e.Date.ToString("yyyy-MM-dd"),e.Kind+" • "+e.Memo,e.MovementId,Money.Display(e.DebitCents),Money.Display(e.CreditCents)})));return Task.CompletedTask;
        }),details);
    }
}
