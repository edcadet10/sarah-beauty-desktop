using Microsoft.UI.Xaml;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class OwnerScheduleView
{
    internal static UIElement Build(ReportSnapshot report)
    {
        if(report.Owners is null)return Form.Text("The owner schedule was not captured in this report version.");
        if(report.Owners.Version!=1)return Form.Text("This owner schedule version requires a compatible app version.");
        var schedule=report.Owners;var panel=Form.Panel();
        panel.Add(Form.Text($"Owner schedule version {schedule.Version} • Approved entries through {report.Through:yyyy-MM-dd}\n"
            +"Amounts follow actual owner identities. Ownership percentages do not divide transactions. Posted balances do not certify tax capital or basis."),
            Period(schedule),Balances(schedule),Loans(schedule),
            Form.Text("Contributions and draws exclude opening entries. Payable added/cleared includes approved opening adjustments and reversals; reimbursements are identified separately."));
        AddSourceReview(panel,schedule,report.From);
        var unknown=schedule.Entries.Where(e=>e.OwnerId.Length==0).ToList();
        if(unknown.Count>0)panel.Add(Form.Text("Some posted owner-account entries lack an owner identity. They have not been divided between the owners."),Entries("Unattributed owner entries",unknown,report.From));
        return panel;
    }

    private static UIElement Period(OwnerScheduleSnapshot schedule)=>PagedReport.Build("Owner period activity",
        ["Owner","Contributions net","Draws net","Payable added","Payable cleared","Reimbursements net","Loan proceeds net","Loan principal paid net"],
        schedule.Positions.Select(p=>new[]{p.Name,Money.Display(p.ContributionsCents),Money.Display(p.DrawsCents),Money.Display(p.PayableAddedCents),
            Money.Display(p.PayableClearedCents),Money.Display(p.ReimbursementsCents),Money.Display(p.LoanProceedsCents),Money.Display(p.LoanPrincipalPaidCents)}));

    private static UIElement Balances(OwnerScheduleSnapshot schedule)=>PagedReport.Build("Owner posted balances",
        ["Owner","Active","Capital credits net","Draws net","Reimbursement payable","Confirmed attributed loan principal"],
        schedule.Positions.Select(p=>new[]{p.Name,p.Active?"Yes":"No",Money.Display(-Closing(schedule,p.OwnerId,"3000")),Money.Display(Closing(schedule,p.OwnerId,"3100")),
            Money.Display(-Closing(schedule,p.OwnerId,"2100")),Money.Display(p.AttributedLoanPrincipalCents)}));

    private static UIElement Loans(OwnerScheduleSnapshot schedule)=>PagedReport.Build("Loan owner attribution",
        ["Current loan name at capture","Principal","Captured owner lender","Attribution status"],
        schedule.LoanBalances.Select(l=>new[]{l.LoanName,l.PrincipalCents.HasValue?Money.Display(l.PrincipalCents.Value):"Unknown",
            schedule.Positions.SingleOrDefault(p=>p.OwnerId==l.OwnerId)?.Name??"Unassigned",l.Attribution}));

    private static void AddSourceReview(Microsoft.UI.Xaml.Controls.StackPanel panel,OwnerScheduleSnapshot schedule,DateOnly from)
    {
        var owner=Form.Records("Supporting owner",schedule.Positions.Select(p=>(p.OwnerId,p.Name)));var details=Form.Panel();var status=Form.Text();
        panel.Add(owner,status,Form.Action("Show captured owner balances and sources",status,()=>
        {
            string id=owner.SelectedId();if(!schedule.Positions.Any(p=>p.OwnerId==id))throw new ArgumentException("Choose an owner from this captured report.");
            details.Children.Clear();details.Add(ReportTable.Build(["Owner account","Opening D−C","Period debits","Period credits","Closing D−C"],
                schedule.Accounts.Where(a=>a.OwnerId==id).Select(a=>new[]{AccountName(a.AccountCode),Money.Display(a.OpeningDebitLessCreditCents),Money.Display(a.PeriodDebitCents),Money.Display(a.PeriodCreditCents),Money.Display(a.ClosingDebitLessCreditCents)})),
                Entries("Owner supporting entries",schedule.Entries.Where(e=>e.OwnerId==id),from));return Task.CompletedTask;
        }),details);
    }

    private static UIElement Entries(string title,IEnumerable<OwnerScheduleEntry> entries,DateOnly from)=>PagedReport.Build(title,
        ["Date / scope","Movement / description","Account","Debit","Credit","Attribution / loan identity"],
        entries.Select(e=>new[]{$"{e.Date:yyyy-MM-dd} • {(e.Date<from?"Before period":"In period")}",
            $"{e.Kind}{(e.Kind=="Reversal"?" of "+e.EffectiveKind:"")} • {e.Memo} • {e.MovementId}",e.AccountCode+" • "+e.AccountName,
            Money.Display(e.DebitCents),Money.Display(e.CreditCents),e.Attribution+(e.LoanId.Length>0?" • "+e.LoanId:"")}));

    private static long Closing(OwnerScheduleSnapshot schedule,string owner,string code)=>schedule.Accounts.Single(a=>a.OwnerId==owner && a.AccountCode==code).ClosingDebitLessCreditCents;
    private static string AccountName(string code)=>code+" • "+(code switch{"3000"=>"Owner capital","3100"=>"Owner draws",_=>"Owner reimbursement payable"});
}
