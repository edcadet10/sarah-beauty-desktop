using Microsoft.UI.Xaml;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class DebtScheduleView
{
    internal static UIElement Build(ReportSnapshot report)
    {
        if(report.Debts is null)return Form.Text("The debt schedule was not captured in this report version.");
        if(report.Debts.Version!=1)return Form.Text("This debt schedule version requires a compatible app version.");
        var schedule=report.Debts;var panel=Form.Panel();var known=schedule.Positions.Where(p=>p.ClosingPrincipalCents.HasValue).ToArray();
        panel.Add(Form.Text($"Debt schedule version {schedule.Version} • Principal as of {report.Through:yyyy-MM-dd}\n"
            +$"Known principal subtotal: {Money.Display(known.Sum(p=>p.ClosingPrincipalCents!.Value))} • Loans without approved principal history: {schedule.Positions.Count-known.Length}"),
            PagedReport.Build("Loan principal balances",["Loan","Posted opening principal","Opening adjustments","Other principal changes","Closing principal"],
                schedule.Positions.Select(p=>new[]{p.CurrentFacts.Name,Amount(p.PostedOpeningPrincipalCents),Money.Display(p.OpeningAdjustmentsCents),Money.Display(p.OtherPrincipalChangeCents),Amount(p.ClosingPrincipalCents)})),
            PagedReport.Build("Loan period activity",["Loan","Proceeds","Principal paid","Interest","Fees"],
                schedule.Positions.Select(p=>new[]{p.CurrentFacts.Name,Money.Display(p.ProceedsCents),Money.Display(p.PrincipalPaidCents),Money.Display(p.InterestCents),Money.Display(p.FeesCents)})),
            Form.Text("Period amounts include dated reversals. Principal is separate from income and expenses. Current register facts do not replace approved openings or establish an older movement's lender."));
        AddSourceReview(panel,schedule,report.From);return panel;
    }

    private static void AddSourceReview(Microsoft.UI.Xaml.Controls.StackPanel panel,DebtScheduleSnapshot schedule,DateOnly from)
    {
        var loan=Form.Records("Supporting loan",schedule.Positions.Select(p=>(p.CurrentFacts.LoanId,p.CurrentFacts.Name)));
        var details=Form.Panel();var status=Form.Text();
        panel.Add(loan,status,Form.Action("Show captured loan facts and sources",status,()=>
        {
            var position=schedule.Positions.SingleOrDefault(p=>p.CurrentFacts.LoanId==loan.SelectedId())??throw new ArgumentException("Choose a loan from this captured schedule.");
            details.Children.Clear();details.Add(Facts(position.CurrentFacts),Sources(schedule,position.CurrentFacts.LoanId,from));return Task.CompletedTask;
        }),details);
    }

    private static UIElement Facts(LoanRegisterFacts facts)=>ReportTable.Build(["Current loan register fact at capture","Value"],
        [["Loan",facts.Name],["Register revision",facts.Revision.ToString()],["Archived",facts.Archived?"Yes":"No"],
        ["Registered opening date",facts.OpeningDate?.ToString("yyyy-MM-dd")??"Unknown"],["Registered opening principal",Amount(facts.OpeningPrincipalCents)],
        ["Current due date",facts.DueDate?.ToString("yyyy-MM-dd")??"Unknown"],["Current terms",Text(facts.Terms)],
        ["Current owner-lender link",facts.OwnerLenderId.Length>0?Text(facts.OwnerLenderName):"No owner-lender link recorded"],["Evidence",Text(facts.EvidenceId)]]);

    private static UIElement Sources(DebtScheduleSnapshot schedule,string loan,DateOnly from)=>PagedReport.Build("Loan supporting entries",
        ["Date / scope","Movement / description","Account","Debit","Credit","Captured lender / owner"],
        schedule.Entries.Where(e=>e.LoanId==loan).Select(e=>new[]{$"{e.Date:yyyy-MM-dd} • {(e.Date<from?"Before period":"In period")}",
            $"{e.Kind}{(e.Kind=="Reversal"?" of "+e.EffectiveKind:"")} • {e.Memo} • {e.MovementId}",e.AccountCode+" • "+e.AccountName,
            Money.Display(e.DebitCents),Money.Display(e.CreditCents),Lender(e.CapturedLender)}));

    private static string Lender(LoanLenderSnapshot? lender)=>lender is null?"Historical lender attribution unconfirmed":
        lender.LenderName+" • "+(lender.OwnerLenderId.Length>0?lender.OwnerLenderName:"No owner-lender link captured");
    private static string Amount(long? cents)=>cents.HasValue?Money.Display(cents.Value):"Unknown";
    private static string Text(string value)=>string.IsNullOrWhiteSpace(value)?"Unknown":value;
}
