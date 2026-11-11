using Microsoft.UI.Xaml;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class ReceiptSimilarityView
{
    internal static UIElement Build(ReceiptSimilarityReview review)
    {
        var panel=Form.Panel();
        if(review.Total==0)return panel;
        panel.Add(Form.Title("Similar receipts to review"),Form.Text($"{review.Total} recorded receipt(s) share this date and gross amount. Compare the actual payment evidence before approving another receipt. Distinct real payments can share these values."),
            PagedReport.Build("Similar receipt evidence",["Date / gross","Cash account / payer","Description / evidence","Approval / record code","Cancellation"],
                review.Receipts.Select(r=>new[]{r.Date.ToString("yyyy-MM-dd")+" • "+Money.Display(r.GrossCents),r.AccountName+" • "+r.PayerName,r.Description+" • "+r.EvidenceId,
                    r.ApprovedByName+" • "+r.ApprovedAtUtc+" • "+r.MovementId[..8],r.CancelledOn?.ToString("yyyy-MM-dd")??"No posting cancellation"})));
        if(review.Total>review.Receipts.Length)panel.Add(Form.Text($"Showing the first {review.Receipts.Length} matching receipts. Review additional entries in Money and the supporting account reports."));
        return panel;
    }
}
