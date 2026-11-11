using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class ReportsView(BusinessStore store,OwnerSession owner)
{
    private readonly FinancialReports _reports=new(store);
    private readonly StackPanel _result=Form.Panel();
    private readonly TextBlock _status=Form.Text();
    private readonly ComboBox _kind=Form.Select("Report",["Profit and loss","Balance sheet","Trial balance","Cash movements","Customer aging","Supplier balances","Tax activity","Asset schedule","Debt schedule"],"Profit and loss");

    internal UIElement Build()
    {
        var panel=Form.Panel();
        var today=DateOnly.FromDateTime(DateTime.Today);
        var from=Form.Input("Period start (YYYY-MM-DD)",new DateOnly(today.Year,today.Month,1).ToString("yyyy-MM-dd"));
        var through=Form.Input("Period end / as-of date",today.ToString("yyyy-MM-dd"));
        panel.Add(Form.Title("Financial reports"),Form.Text("Management books use approved cash-method classifications. Invoices and future bookings are operational balances, not received income."),
            _kind,from,through,_status,Form.Action("Run report",_status,async()=>
            {
                var first=Input.Date(from.Text)??throw new ArgumentException("Enter the period start.");
                var last=Input.Date(through.Text)??throw new ArgumentException("Enter the period end.");
                var report=await Task.Run(()=>_reports.Load(owner,first,last));
                Show(report);
            }),_result);
        return panel;
    }

    private void Show(ReportSnapshot report)
    {
        _result.Children.Clear();
        _result.Add(Form.Title((string)_kind.SelectedItem),Form.Text($"{report.BusinessName} • {report.Mode} • Management cash-method policy\n"
            +$"Period {report.From:yyyy-MM-dd} through {report.Through:yyyy-MM-dd} • Watermark {report.Watermark}\nGenerated {report.GeneratedAtUtc}"));
        if(report.OpeningWarnings.Length>0)_result.Add(Form.Text("Unconfirmed inputs: "+string.Join("; ",report.OpeningWarnings)));
        _result.Add(Table(report,_kind.SelectedItem as string));
        if(_kind.SelectedItem as string=="Supplier balances")_result.Add(Form.Text("Supporting supplier bills"),
            ReportTable.Build(["Bill","Purchase date","Due date","Unpaid","Supplier credit"],report.SupplierBills.Select(b=>new[]{b.Name,b.PurchaseDate.ToString("yyyy-MM-dd"),b.DueDate?.ToString("yyyy-MM-dd")??"Unknown",Money.Display(b.PayableCents),Money.Display(b.CreditCents)})));
        if(_kind.SelectedItem as string=="Balance sheet")_result.Add(Form.Text(report.BalanceSheet.Balances?"Assets equal liabilities plus equity including result.":"Balance-sheet equality failed. Review the source ledger."));
        SupportingEntries(report);
    }

    internal static UIElement Table(ReportSnapshot r,string? kind)=>kind switch
    {
        "Balance sheet"=>ReportTable.Build(["Balance as of period end","USD"],
            [["Assets",Money.Display(r.BalanceSheet.AssetsCents)],["Liabilities",Money.Display(r.BalanceSheet.LiabilitiesCents)],["Equity including result",Money.Display(r.BalanceSheet.EquityCents)]]),
        "Trial balance"=>ReportTable.Build(["Account","Opening D−C","Debits","Credits","Ending D−C"],r.TrialBalance.Select(a=>new[]{a.Account.ToString(),Money.Display(a.Opening),Money.Display(a.Debits),Money.Display(a.Credits),Money.Display(a.Ending)})),
        "Cash movements"=>ActivityTable(r.Cash),
        "Customer aging"=>ReportTable.Build(["Payer","Open","Overdue","Due today","Future","Date unknown","Unallocated income receipts","Held deposits","Unused credits"],r.Customers.Select(c=>new[]{c.Name,Money.Display(c.OpenCents),Money.Display(c.OverdueCents),Money.Display(c.DueTodayCents),Money.Display(c.FutureCents),Money.Display(c.UnknownDueCents),Money.Display(c.UnallocatedReceiptsCents),Money.Display(c.HeldReceiptsCents),Money.Display(c.UnusedCreditCents)})),
        "Supplier balances"=>ReportTable.Build(["Supplier","Unpaid","Overdue","Due today","Future","Date unknown","Supplier credit"],r.Suppliers.Select(s=>new[]{s.Name,Money.Display(s.OpenCents),Money.Display(s.OverdueCents),Money.Display(s.DueTodayCents),Money.Display(s.FutureCents),Money.Display(s.UnknownDueCents),Money.Display(s.CreditCents)})),
        "Tax activity"=>TaxTable(r.Tax),
        "Asset schedule"=>AssetScheduleView.Build(r),
        "Debt schedule"=>DebtScheduleView.Build(r),
        _=>ReportTable.Build(["Period result","USD"],[["Income",Money.Display(r.ProfitLoss.IncomeCents)],["Expenses",Money.Display(r.ProfitLoss.ExpenseCents)],["Result",Money.Display(r.ProfitLoss.ResultCents)]])
    };

    private static UIElement TaxTable(TaxPosition tax)=>ReportTable.Build(["Tax measure","USD"],
        [["Charged on issued invoices",Money.Display(tax.ChargedCents)],["Charge reductions",Money.Display(tax.ChargeReductionCents)],
        ["Collected, net of reversals",Money.Display(tax.CollectedCents)],["Refunded to customers, net of reversals",Money.Display(tax.RefundedCents)],
        ["Remitted, net of reversals",Money.Display(tax.RemittedCents)],["Liability as of end",Money.Display(tax.LiabilityCents)],
        ["Recoverable tax as of end",Money.Display(tax.RecoverableCents)],["Actual cash recovered in period",Money.Display(tax.RecoveredCashCents)],
        ["Actual liability offsets in period",Money.Display(tax.OffsetCents)],["Nonrecoverable tax expense in period",Money.Display(tax.NonrecoverableCents)]]);

    private void SupportingEntries(ReportSnapshot report)
    {
        var account=Form.Records("Supporting account",report.TrialBalance.Select(a=>(a.Account.Id,a.Account.ToString())));
        var detail=Form.Panel();
        var status=Form.Text();
        _result.Add(account,status,Form.Action("Show posted account entries for this period",status,()=>
        {
            string id=account.SelectedId();
            if(id.Length==0)throw new ArgumentException("Choose a supporting account.");
            detail.Children.Clear();
            detail.Add(ActivityTable(report.Entries.Where(e=>e.AccountId==id)));
            return Task.CompletedTask;
        }),detail);
    }

    private static UIElement ActivityTable(IEnumerable<CashActivity> rows)=>ReportTable.Build(["Date / movement","Account","Debit","Credit"],
        rows.Select(c=>new[]{$"{c.Date:yyyy-MM-dd} • {c.Kind} • {c.Memo}",c.AccountName,Money.Display(c.DebitCents),Money.Display(c.CreditCents)}));
}
