using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;
using Windows.Graphics.Imaging;

namespace SarahBeauty_Desktop.Views;

internal static class EvidenceContentView
{
    internal static UIElement Build(BusinessStore store,OwnerSession owner,AttachmentVersion version,string linkId="")
    {
        var panel=Form.Panel();var result=new ContentControl();var status=Form.Text();
        Stream Open()=>linkId.Length==0?new ManagedAttachments(store).OpenRead(owner,version.Id):new EvidenceLinks(store).OpenRead(owner,linkId);
        panel.Unloaded+=(_,_)=>result.Content=null;
        panel.Add(status,Form.Action("Verify managed file integrity",status,async()=>
        {
            await Task.Run(()=>{using var stream=Open();});status.Text=$"Verified {version.Name} v{version.Number} • {version.ByteLength} bytes • Current access permitted.";
        }),Form.Action(version.MediaType=="application/pdf"?"Verify and open managed PDF":"Verify and preview private image",status,async()=>
        {
            using var stream=await Task.Run(Open);
            if(version.MediaType=="application/pdf")
            {
                if(!panel.IsLoaded)return;string path=((FileStream)stream).Name;Process.Start(new ProcessStartInfo(path){UseShellExecute=true});status.Text="Verified PDF opened in your PDF viewer.";return;
            }
            using var random=stream.AsRandomAccessStream();var decoder=await BitmapDecoder.CreateAsync(random);
            if(decoder.PixelWidth==0||decoder.PixelHeight==0||decoder.PixelWidth>20000||decoder.PixelHeight>20000||(ulong)decoder.PixelWidth*decoder.PixelHeight>100000000)
                throw new ArgumentException("Use a preview image up to 100 megapixels and 20,000 pixels per side.");
            var bitmap=new BitmapImage();if(decoder.PixelWidth>=decoder.PixelHeight)bitmap.DecodePixelWidth=(int)Math.Min(1280,decoder.PixelWidth);else bitmap.DecodePixelHeight=(int)Math.Min(1280,decoder.PixelHeight);
            random.Seek(0);await bitmap.SetSourceAsync(random);if(!panel.IsLoaded)return;
            var image=new Image{Source=bitmap,MaxHeight=600,Stretch=Stretch.Uniform};AutomationProperties.SetName(image,"Private managed image preview");result.Content=image;
            status.Text="Verified private image preview. Permission is checked again for each open; leaving this section clears the preview.";
        }),result);return panel;
    }
}
