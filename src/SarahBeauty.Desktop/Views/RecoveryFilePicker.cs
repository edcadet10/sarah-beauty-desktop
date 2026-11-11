using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;

namespace SarahBeauty_Desktop.Views;

internal static class RecoveryFilePicker
{
    internal static async Task<string?> Folder()
    {
        var window=((App)Application.Current).ActiveWindow;var picker=new FolderPicker(window.AppWindow.Id){SuggestedStartLocation=PickerLocationId.DocumentsLibrary};return(await picker.PickSingleFolderAsync())?.Path;
    }
    internal static async Task<string?> Save(string name,string extension,string label)
    {
        var window=((App)Application.Current).ActiveWindow;var picker=new FileSavePicker(window.AppWindow.Id){SuggestedStartLocation=PickerLocationId.DocumentsLibrary,SuggestedFileName=name,DefaultFileExtension=extension};
        picker.FileTypeChoices.Add(label,new List<string>{extension});return(await picker.PickSaveFileAsync())?.Path;
    }
    internal static async Task<string?> OpenRecovery()
    {
        var window=((App)Application.Current).ActiveWindow;var picker=new FileOpenPicker(window.AppWindow.Id){SuggestedStartLocation=PickerLocationId.DocumentsLibrary};
        foreach(string extension in new[]{".sbrecovery",".age",".txt",".agekey"})picker.FileTypeFilter.Add(extension);return(await picker.PickSingleFileAsync())?.Path;
    }
    internal static async Task<string?> OpenArchive()
    {
        var window=((App)Application.Current).ActiveWindow;var picker=new FileOpenPicker(window.AppWindow.Id){SuggestedStartLocation=PickerLocationId.DocumentsLibrary};
        picker.FileTypeFilter.Add(".age");return(await picker.PickSingleFileAsync())?.Path;
    }
}
