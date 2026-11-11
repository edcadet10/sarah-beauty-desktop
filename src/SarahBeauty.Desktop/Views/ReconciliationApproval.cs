using Microsoft.UI.Xaml;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Security;

namespace SarahBeauty_Desktop.Views;

internal static class ReconciliationApproval
{
    internal static UIElement Build(Reconciliations book,OwnerSession owner,ReconciliationPreview preview,Action<CompletedReconciliation> completed)
    {
        var panel=Form.Panel();panel.Add(ReconciliationSnapshotView.Build(preview));
        if(!preview.ReadyToComplete)return panel;
        var status=Form.Text();var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase to complete reconciliation");
        string request=Guid.NewGuid().ToString();
        panel.Add(phrase,status,Form.Action("Complete reviewed reconciliation",status,async()=>
        {
            string password=phrase.Password;phrase.Password="";
            var saved=await Task.Run(()=>book.Complete(owner,preview.Draft,preview.Sources.Watermark,request,password));
            completed(saved);
        }));
        return panel;
    }
}
