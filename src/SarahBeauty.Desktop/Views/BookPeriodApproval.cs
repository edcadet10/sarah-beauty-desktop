using Microsoft.UI.Xaml;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;

namespace SarahBeauty_Desktop.Views;

internal static class BookPeriodApproval
{
    internal static UIElement Build(BookPeriods book,OwnerSession owner,BookPeriodPreview preview,IReadOnlyDictionary<string,string> owners,Action<ClosedBookPeriod> completed)
    {
        var panel=Form.Panel();panel.Add(BookPeriodSnapshotView.Build(preview,owners));
        if(!preview.ReadyToClose){panel.Add(Form.Text("Resolve the listed issues before closing this period."));return panel;}
        var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase to close the book period");var status=Form.Text();string request=Guid.NewGuid().ToString();
        panel.Add(Form.Text("Ready for owner confirmation to close the book period. Approval uses this captured review; refresh it after changing the closing fields."),phrase,status,
            Form.Action("Close reviewed book period",status,async()=>
            {
                string password=phrase.Password;phrase.Password="";
                var saved=await Task.Run(()=>book.Close(owner,preview.Draft,preview.Reports.Watermark,request,password));
                completed(saved);
            }));
        return panel;
    }
}
