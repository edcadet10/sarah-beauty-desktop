using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Security;

namespace SarahBeauty_Desktop.Views;

internal static class CancellationApprovalView
{
    internal static UIElement Build(CancellationCases book,OwnerSession owner,CancellationPreview preview,Action<ApprovedCancellation> saved)
    {
        var panel=Form.Panel();var status=Form.Text();
        var confirmed=new CheckBox{Content="I reviewed the agreement, all effects, and remaining invoice obligations",MinHeight=44};
        var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase to approve cancellation");
        panel.Add(Form.Text("Approval records the selected actual refund and credit actions, cancels booked work, and retains the complete decision. It does not send money through a bank or contact the customer."),
            confirmed,phrase,status,Form.Action("Approve combined cancellation",status,async()=>
            {
                if(confirmed.IsChecked!=true)throw new ArgumentException("Review and confirm the complete agreement, effects, and remaining obligations first.");
                string password=phrase.Password;phrase.Password="";
                var result=await Task.Run(()=>book.Approve(owner,preview,password));saved(result);
            }));return panel;
    }
}
