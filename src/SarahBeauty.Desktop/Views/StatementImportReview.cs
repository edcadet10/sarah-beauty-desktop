using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;

namespace SarahBeauty_Desktop.Views;

internal static class StatementImportReview
{
    internal static UIElement Build(StatementBook book,OwnerSession owner,StatementImportPreview preview,string accountName,Action<StatementImport> imported)
    {
        var panel=Form.Panel();
        var status=Form.Text();
        var d=preview.Draft;
        panel.Add(Form.Title("Review statement import"),Form.Text($"{accountName} • {d.From:yyyy-MM-dd} – {d.Through:yyyy-MM-dd} • Opening {Money.Display(d.OpeningCents!.Value)} • Closing {Money.Display(d.ClosingCents!.Value)} • {preview.Rows.Length} source rows"),
            Form.Text("This review uses the source, mapping, account, and balances captured when you selected Review statement import. Changes in the form require a new review."));
        if(d.EvidenceId.Length>0)panel.Add(Form.Text("Statement evidence: "+d.EvidenceId));
        if(preview.ExistingImportId.Length>0)
        {
            panel.Add(Form.Text("This CSV content is already imported. Open the existing statement below to review its retained source rows."));
            return panel;
        }
        var rows=new StatementReviewRows(preview.Rows);
        var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase to import");
        string request=Guid.NewGuid().ToString();
        panel.Add(rows.Build(),Form.Text("Approval saves these reviewed statement records. It does not post cash or income. Matching and reconciliation use separate decisions."),phrase,status,
            Form.Action("Import reviewed statement",status,async()=>
            {
                var choices=rows.Read();
                string password=phrase.Password;phrase.Password="";
                var saved=await Task.Run(()=>book.Import(owner,d,preview.Generation,choices,request,password));
                imported(saved);
            }));
        return panel;
    }
}
