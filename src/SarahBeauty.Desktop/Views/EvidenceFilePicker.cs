using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;

namespace SarahBeauty_Desktop.Views;

internal static class EvidenceFilePicker
{
    internal static async Task<string?> Pick()
    {
        var window=((App)Application.Current).ActiveWindow;var picker=new FileOpenPicker(window.AppWindow.Id){SuggestedStartLocation=PickerLocationId.DocumentsLibrary};
        foreach(string type in new[]{".pdf",".jpg",".jpeg",".png"})picker.FileTypeFilter.Add(type);
        return (await picker.PickSingleFileAsync())?.Path;
    }
}
