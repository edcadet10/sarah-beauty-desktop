using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class ResourceView
{
    private void Edit(Loan loan)
    {
        _detail.Children.Clear();
        var name=Form.Input("Lender / loan name",loan.Name);
        var date=Form.Input("Opening date (YYYY-MM-DD; blank if unknown)",loan.OpeningDate?.ToString("yyyy-MM-dd")??"");
        var principal=Form.Input("Opening principal in USD (blank means unknown; zero means confirmed zero)",loan.OpeningPrincipalCents.HasValue?Money.Input(loan.OpeningPrincipalCents.Value):"");
        var due=Form.Input("Due date (optional)",loan.DueDate?.ToString("yyyy-MM-dd")??"");
        var lender=Form.Records("Owner lender (only for an actual owner loan)",new OwnerAccess(store).Owners(),loan.OwnerLenderId);
        lender.IsEnabled=!_book.LoanLenderLocked(owner,loan.Id);
        var terms=Form.Input("Loan terms (informational)",loan.Terms);
        var evidence=Form.Input("Evidence reference",loan.EvidenceId);
        var archived=Archived(loan.Archived);
        var status=Form.Text();
        _detail.Add(Form.Title("Loan"),Form.Text("Saving principal here does not create a ledger opening. An approved opening is required for a nonzero starting loan; new proceeds and repayments must also be approved."),
            name,date,principal,due,lender,Form.Text("The owner-lender link is protected after posting. Each new approved loan movement retains its reviewed lender facts."),terms,evidence,archived,status,Form.Action("Save loan",status,async()=>
            {
                var value=loan with{Name=name.Text,OpeningDate=Input.Date(date.Text),OpeningPrincipalCents=OptionalMoney(principal.Text),
                    DueDate=Input.Date(due.Text),OwnerLenderId=lender.SelectedId(),Terms=Form.ReadInput(terms,loan.Terms),EvidenceId=Form.ReadInput(evidence,loan.EvidenceId).Trim(),Archived=archived.IsChecked==true};
                var saved=await Task.Run(()=>_book.SaveLoan(owner,value));
                Refresh();
                Edit(saved);
            }));
    }
}
