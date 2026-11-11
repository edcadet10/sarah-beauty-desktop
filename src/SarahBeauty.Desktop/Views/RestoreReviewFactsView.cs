using Microsoft.UI.Xaml;
using SarahBeauty.Business.Recovery;

namespace SarahBeauty_Desktop.Views;

internal static class RestoreReviewFactsView
{
    internal static UIElement Build(StagedArchiveReview review)
    {
        var m=review.Manifest;var panel=Form.Panel();panel.Add(Form.Title("Validated archive review"),
            Form.Text("The staged database and managed files passed validation. The working business store has not been switched. Restore activation and second-device acceptance remain separate."),
            ReportTable.Build(["Verified recovery fact","Value"],[["Recorded backup matched",review.MatchedRecordedArchive?"Yes":"No local backup record supplied"],["Business",m.BusinessName],["Business identity",m.BusinessId],["Store identity",m.StoreId],
            ["Mode / schema",m.Mode+" / "+m.SchemaVersion],["Captured time / watermark",m.CreatedAtUtc+" / "+m.Watermark],["Encrypted bytes / SHA-256",review.ArchiveBytes+" / "+review.ArchiveHash],["Manifest SHA-256",review.ManifestHash],
            ["Movements / journal lines",m.Facts.MovementCount+" / "+m.Facts.JournalLineCount],["Journal debit / credit cents",m.Facts.TotalDebits+" / "+m.Facts.TotalCredits]]),
            PagedReport.Build("Validated restored file manifest",["Managed path","Kind","Bytes","SHA-256"],m.Files.Select(f=>new[]{f.RelativePath,f.Kind,f.ByteLength.ToString(),f.Sha256})),
            PagedReport.Build("Validated restored table counts",["Table","Rows"],m.Facts.Tables.Select(t=>new[]{t.Table,t.Rows.ToString()})));
        return panel;
    }
}
