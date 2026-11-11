using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal static class FinancialEvidenceView
{
    internal static UIElement Build(BusinessStore store,OwnerSession owner,ManagedEvidenceFile evidence)
    {
        var panel=Form.Panel();var detail=new ContentControl();var status=Form.Text();
        panel.Add(Form.Text($"Captured supporting file: {evidence.Name} • v{evidence.Number} • {evidence.Category}\n"
            +$"Original name: {evidence.OriginalFileName} • {evidence.ByteLength} bytes\nVersion {evidence.VersionId} • Content SHA-256 {evidence.Sha256}\nMetadata SHA-256 {evidence.MetadataHash}"),
            status,Form.Action("Review captured evidence file",status,async()=>
            {
                var file=await Task.Run(()=>new FinancialEvidence(store).Read(owner,evidence));if(!panel.IsLoaded)return;
                detail.Content=EvidenceContentView.Build(store,owner,file.Version);
            }),detail);return panel;
    }
}
