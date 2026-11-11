using Microsoft.UI.Xaml;
using SarahBeauty.Business.Recovery;

namespace SarahBeauty_Desktop.Views;

internal static class LocalBackupDetailView
{
    internal static UIElement Build(LocalBackupJob job)
    {
        var panel=Form.Panel();var result=job.Result;
        panel.Add(ReportTable.Build(["Archive fact","Recorded value"],[["Job identity",job.Id],["Last reported status",job.Status],["Destination",job.Destination],["Started / finished",job.StartedAtUtc+" / "+job.FinishedAtUtc],
            ["Source instance",job.InstanceId??"Unconfirmed historical instance"],["Automatic schedule / revision",job.AutomaticScheduleId is null?"Manual":job.AutomaticScheduleId+" / "+job.AutomaticScheduleRevision],
            ["Requested watermark",job.RequestedWatermark.ToString()],["Captured watermark",result?.Manifest.Watermark.ToString()??"Not completed"],["Recipient fingerprint",job.RecipientFingerprint],
            ["Encrypted bytes / SHA-256",result is null?"Not completed":result.ByteLength+" / "+result.Sha256],["Manifest SHA-256",result?.ManifestHash??"Not completed"],["Failure code",job.FailureCode??"None"]]));
        if(result is not null)
        {
            var m=result.Manifest;panel.Add(Form.Text($"{m.BusinessName} • {m.Mode} • Store {m.StoreId} • Schema {m.SchemaVersion}\nCaptured {m.CreatedAtUtc} • Movements {m.Facts.MovementCount} • Journal lines {m.Facts.JournalLineCount}\nJournal debit / credit cents: {m.Facts.TotalDebits} / {m.Facts.TotalCredits}"),
                PagedReport.Build("Archived file manifest",["Managed path","Kind","Bytes","SHA-256"],m.Files.Select(f=>new[]{f.RelativePath,f.Kind,f.ByteLength.ToString(),f.Sha256})));
        }
        return panel;
    }
}
