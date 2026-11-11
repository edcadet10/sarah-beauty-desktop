using Microsoft.UI.Xaml;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal static class StatementWithdrawalView
{
    internal static UIElement Build(BusinessStore store,OwnerSession owner,StatementImport statement,Action refreshed)
    {
        var book=new StatementWithdrawals(store);var panel=Form.Panel();
        if(statement.Withdrawn)
        {
            var names=new OwnerAccess(store).Owners().ToDictionary(o=>o.Id,o=>o.Name);
            var decision=book.History(owner).Single(w=>w.StatementId==statement.Id);
            panel.Add(Form.Title("Withdrawn statement import"),Form.Text($"Withdrawn by {names.GetValueOrDefault(decision.OwnerId,"Former owner")} at {decision.AtUtc}."),
                Form.Text("Withdrawal reason: "+decision.Reason),Form.Text("Withdrawal evidence: "+decision.EvidenceId),
                Form.Text("The original header and source rows remain below. Import the corrected statement after reviewing its actual mapping and balances."));
            return panel;
        }
        var review=book.Review(owner,statement.Id);
        var reason=Form.Input("Reason for withdrawing this incorrect import");
        var evidence=Form.Input("Evidence for the corrected statement or mapping");
        var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase to withdraw the import");var status=Form.Text();
        string request=Guid.NewGuid().ToString();
        panel.Add(Form.Title("Correct an imported statement"),
            Form.Text($"Unshared bank lines: {review.UnsharedLineCount}. Lines still matched, including future matching: {review.MatchedLineCount}."),
            Form.Text("Withdrawal preserves this source and records your reason. Reopen covered book periods and reconciliations first, then unmatch the otherwise unreferenced lines. Shared transactions remain active. Withdrawal creates no cash movement. Import the corrected source afterward."),
            reason,evidence,phrase,status,Form.Action("Withdraw incorrect statement import",status,async()=>
            {
                string why=reason.Text,reference=evidence.Text,password=phrase.Password;phrase.Password="";
                await Task.Run(()=>book.Approve(owner,statement.Id,review.Generation,why,reference,request,password));
                refreshed();
            }));
        return panel;
    }
}
