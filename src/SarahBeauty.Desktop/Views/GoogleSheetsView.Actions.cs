using System.Globalization;
using Microsoft.UI.Xaml;
using SarahBeauty.Business.Google;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class GoogleSheetsView
{
    private async Task Run(Func<CancellationToken,Task> action)
    {
        if(_cancellation is not null)throw new ArgumentException("Wait for the current readable operation.");_cancellation=new();_fields.IsEnabled=false;_progress.Visibility=Visibility.Visible;
        try{await action(_cancellation.Token);}
        catch(OperationCanceledException){if(_loaded){_status.Text="Readable operation cancelled. Existing files are retained; refresh health and history before retrying.";TryRefresh();}}
        catch{if(_loaded)TryRefresh();throw;}
        finally{_fields.IsEnabled=true;_progress.Visibility=Visibility.Collapsed;_cancellation.Dispose();_cancellation=null;}
    }
    private void TryRefresh(){try{Refresh();}catch(Exception){}}
    private async Task CreateFolder(CancellationToken cancellation)
    {
        var connection=new GoogleConnections(store).Current(owner);if(connection.Status!="Connected"||connection.LastConnection is null)throw new ArgumentException("Connect and verify your Google account first.");
        int revision=_policy?.Revision??0;_status.Text="Creating and verifying a private Google output folder…";
        await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>new GoogleOutputSettings(store).CreateFolderAsync(owner,connection.LastConnection.Id,revision,token),token),cancellation);
        if(!_loaded)return;ReloadPermission();Refresh();_status.Text="Private output folder verified. Sheets and archive delivery are off until you save their choices again.";
    }
    private async Task SavePermission(CancellationToken cancellation)
    {
        var policy=_policy??throw new ArgumentException("Create and verify a private Google output folder first.");var draft=new GoogleOutputDraft(policy.DriveArchivesEnabled,_enabled.IsChecked==true,policy.Revision);
        await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>new GoogleOutputSettings(store).SaveAsync(owner,draft,token),token),cancellation);
        if(!_loaded)return;ReloadPermission();Refresh();_status.Text=draft.SheetsExportEnabled?"Readable Sheets delivery enabled. Automatic delivery also needs a saved enabled schedule.":"Readable Sheets delivery disabled. Existing workbooks are retained.";
    }
    private async Task SaveProfile(CancellationToken cancellation)
    {
        if(!int.TryParse(_interval.Text,NumberStyles.None,CultureInfo.InvariantCulture,out int minutes))throw new ArgumentException("Enter a whole number of minutes for readable export.");
        var draft=new GoogleSheetsProfileDraft(Dates(),_automatic.IsChecked==true,minutes,_profile?.Revision??0);
        await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>new GoogleSheetsExportProfiles(store).Save(owner,draft,token),token),cancellation);
        if(!_loaded)return;ReloadProfile();Refresh();_status.Text=draft.AutomaticEnabled?"Readable dates and automatic choice saved. Delivery also requires the saved Google Sheets choice.":"Readable dates saved and automatic export disabled. Existing workbooks are retained.";
    }
    private async Task Preview(CancellationToken cancellation)
    {
        var dates=Dates();_status.Text="Building and verifying the local readable preview…";
        var preview=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>new GoogleSheetsPreview(store).Build(owner,dates,token),token),cancellation);
        if(!_loaded)return;ShowPreview(preview);_status.Text="Local readable preview complete. No Google request or export job was created.";
    }
    private async Task Export(CancellationToken cancellation)
    {
        RequireEnabled();var dates=Dates();_status.Text="Writing and independently checking the readable workbook. This can take several minutes…";
        var attempt=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>new GoogleSheetsDeliveryWorker(store).ExportAsync(owner,dates,token),token),cancellation);
        if(!_loaded)return;Refresh();_status.Text="Readable workbook verified and completion recorded: "+attempt.Status+". Later desktop changes remain separate.";
    }
    private async Task Retry(CancellationToken cancellation)
    {
        string id=Selected().Job.Id;RequireEnabled();_status.Text="Resuming and independently verifying the saved readable job…";
        var attempt=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>new GoogleSheetsDeliveryWorker(store).RetryAsync(owner,id,token),token),cancellation);
        if(!_loaded)return;Refresh();_status.Text="Saved readable job verified: "+attempt.Status+".";
    }
    private void RequireEnabled()
    {
        if(_policy?.SheetsExportEnabled!=true)throw new ArgumentException("Create a private output folder and save the enabled Google Sheets choice first.");
    }
}
