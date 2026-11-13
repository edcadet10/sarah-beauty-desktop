using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace SarahBeauty_Desktop.Views;

internal sealed class WorkspaceExitReview(FrameworkElement host)
{
    private ContentDialog? _dialog;

    internal async Task<bool> ConfirmAsync(string title,string action)
    {
        if(host.XamlRoot is null||_dialog is not null)return false;
        if(VisualTreeHelper.GetOpenPopupsForXamlRoot(host.XamlRoot).Any(p=>ContainsDialog(p.Child)))return false;
        _dialog=new()
        {
            XamlRoot=host.XamlRoot,Title=title,
            Content="Any unfinished fields in all sections will be cleared. Save your work first if you want to keep it. Saved records are retained.",
            PrimaryButtonText=action,CloseButtonText="Keep working",DefaultButton=ContentDialogButton.Close
        };
        try{return await _dialog.ShowAsync()==ContentDialogResult.Primary;}
        catch(Exception error) when(error is InvalidOperationException or System.Runtime.InteropServices.COMException){return false;}
        finally{_dialog=null;}
    }

    internal void Cancel()=>_dialog?.Hide();

    private static bool ContainsDialog(DependencyObject? root)
    {
        if(root is null)return false;
        if(root is ContentDialog)return true;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
            if(ContainsDialog(VisualTreeHelper.GetChild(root,i)))return true;
        return false;
    }
}
