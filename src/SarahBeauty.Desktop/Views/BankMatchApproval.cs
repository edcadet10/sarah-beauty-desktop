using Microsoft.UI.Xaml;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Security;

namespace SarahBeauty_Desktop.Views;

internal static class BankMatchApproval
{
    internal static UIElement Build(BankMatching book,OwnerSession owner,BankMatchPreview preview,string account,IEnumerable<string> descriptions,Func<Task> approved)
    {
        var panel=Form.Panel();
        var status=Form.Text();
        var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase to approve matching");
        string request=Guid.NewGuid().ToString();
        panel.Add(Form.Title("Review bank matching"),Form.Text($"{account} • Effective {preview.Draft.Date:yyyy-MM-dd}\n{preview.Draft.Reason}"));
        foreach(string description in descriptions)panel.Add(Form.Text(description));
        panel.Add(Form.Text("This approves the captured matching decisions. It changes no cash, income, or expense amount."),phrase,status,
            Form.Action("Approve reviewed matching",status,async()=>
            {
                string password=phrase.Password;phrase.Password="";
                await Task.Run(()=>book.Approve(owner,preview.Draft,preview.Generations,request,password));
                await approved();
            }));
        return panel;
    }
}
