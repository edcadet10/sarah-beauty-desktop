using System.Text;
using Microsoft.UI.Xaml;
using SarahBeauty.Business.Banking;
using Microsoft.Windows.Storage.Pickers;

namespace SarahBeauty_Desktop.Views;

internal static class StatementFilePicker
{
    internal static async Task<string?> ReadCsv()
    {
        var window=((App)Application.Current).ActiveWindow;
        var picker=new FileOpenPicker(window.AppWindow.Id){SuggestedStartLocation=PickerLocationId.DocumentsLibrary};
        picker.FileTypeFilter.Add(".csv");
        var file=await picker.PickSingleFileAsync();
        if(file is null)return null;
        using var stream=File.OpenRead(file.Path);
        if(stream.Length>StatementCsv.MaximumBytes)throw new ArgumentException("Choose a UTF-8 CSV file of at most 4 MB.");
        using var reader=new StreamReader(stream,new UTF8Encoding(false,true),detectEncodingFromByteOrderMarks:false);
        return await reader.ReadToEndAsync();
    }
}
