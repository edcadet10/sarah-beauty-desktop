using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal static class PdfArtifactView
{
    internal static UIElement Build(BusinessStore store,OwnerSession owner,string versionId)
    {
        var artifacts=new DocumentArtifacts(store);
        var info=artifacts.Get(owner,versionId);
        var panel=Form.Panel();
        var status=Form.Text($"PDF: {info.Status}"+(info.Error is null?"":" • "+info.Error));
        var open=Form.Action("Open saved PDF",status,()=>
        {
            string path=artifacts.VerifiedPath(owner,versionId);
            Process.Start(new ProcessStartInfo(path){UseShellExecute=true});
            return Task.CompletedTask;
        });
        open.IsEnabled=info.Status=="Succeeded";
        Button? render=null;
        render=Form.Action("Create / retry PDF",status,async()=>
        {
            await Task.Run(()=>artifacts.Render(owner,versionId));
            status.Text="PDF ready. You can open, print, or manually share the saved copy.";
            open.IsEnabled=true;
        });
        panel.Add(status,render,open);
        return panel;
    }
}
