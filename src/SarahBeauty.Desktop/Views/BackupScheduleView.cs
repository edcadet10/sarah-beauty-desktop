using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class BackupScheduleView(BusinessStore store,OwnerSession owner,Func<string>? runtime)
{
    private readonly CheckBox _enabled=new(){Content="Enable automatic local archives while the app is open"};
    private readonly TextBox _interval=Form.Input("Archive interval in minutes (1–10080)","30");
    private readonly TextBlock _folder=Form.Text("Archive folder: Not selected"),_space=Form.Text(),_queue=Form.Text(),_runtime=Form.Text(),_status=Form.Text();
    private readonly ContentControl _fields=new();
    private readonly DispatcherTimer _timer=new(){Interval=TimeSpan.FromSeconds(2)};
    private string _directory="";
    private int _revision;
    private bool _loaded,_initialized;
    internal UIElement Build()
    {
        var fields=Form.Panel();fields.Add(_enabled,_interval,_folder,Form.Action("Choose automatic archive folder",_status,ChooseFolder),Form.Action("Estimate archive space",_status,Estimate),_space,Form.Action("Save automatic archive schedule",_status,Save));_fields.Content=fields;
        var panel=Form.Panel();panel.Add(Form.Text("Automatic archives use the confirmed recovery key and run while Sarah Beauty is open, including when owner access is locked. Earlier archives are retained. Copy archives to another device or private storage for device-loss protection."),_fields,
            Form.Action("Reload saved archive schedule",_status,()=>{Reload();return Task.CompletedTask;}),Form.Action("Refresh automatic archive status",_status,()=>{Refresh();return Task.CompletedTask;}),_queue,_runtime,_status);
        panel.Loaded+=(_,_)=>{_loaded=true;try{if(!_initialized){Reload();_initialized=true;}else Refresh();}catch(Exception error){_status.Text=error.Message;}_timer.Start();};
        panel.Unloaded+=(_,_)=>{_loaded=false;_timer.Stop();};_timer.Tick+=(_,_)=>ShowRuntime();
        return new Expander{Header="Automatic local archives",Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch};
    }
    private void Reload()
    {
        var saved=new BackupSchedules(store).Current(owner);_revision=saved?.Revision??0;_enabled.IsChecked=saved?.Enabled??false;_interval.Text=(saved?.IntervalMinutes??30).ToString();_directory=saved?.Directory??"";ShowFolder();_space.Text="";Refresh();
    }
    private void Refresh()
    {
        var state=new AutomaticBackups(store).Current(owner);_queue.Text=state is null?"Automatic archive queue: Never observed.":$"Automatic archive queue: {state.Status} • Pending watermark {state.PendingWatermark} • Archived {state.ArchivedWatermark?.ToString()??"None"}\nNext eligible attempt {state.NextAttemptAtUtc??"Now"} • Consecutive failures {state.FailureCount} • Last code {state.FailureCode??"None"}";ShowRuntime();
    }
    private void ShowRuntime()=>_runtime.Text="Automatic worker: "+(runtime?.Invoke()??"Unavailable");
    private void ShowFolder()=>_folder.Text="Archive folder: "+(_directory.Length==0?"Not selected":_directory);
    private async Task ChooseFolder()
    {
        _fields.IsEnabled=false;
        try{string? selected=await RecoveryFilePicker.Folder();if(!_loaded || selected is null)return;_directory=selected;ShowFolder();_space.Text="";}
        finally{_fields.IsEnabled=true;}
    }
    private async Task<ArchiveSpaceEstimate> CaptureEstimate()
    {
        string directory=_directory;if(string.IsNullOrWhiteSpace(directory))throw new ArgumentException("Choose an existing private archive folder.");var estimate=await Task.Run(()=>new ArchiveCapacity(store).Estimate(owner,directory));
        if(!_loaded)throw new OperationCanceledException("The schedule view closed.");
        _space.Text=$"Uncompressed source: {estimate.SourceBytes:N0} bytes • Archive allowance: {estimate.ArchiveAllowanceBytes:N0} bytes • Managed files: {estimate.ManagedFileCount}\n"+
            string.Join("\n",estimate.Volumes.Select(v=>$"{v.Root} • Working allowance {v.RequiredBytes:N0} bytes • Available {(v.AvailableBytes is long free?free.ToString("N0")+" bytes":"Unknown")}"));return estimate;
    }
    private async Task Estimate(){_fields.IsEnabled=false;try{await CaptureEstimate();}finally{_fields.IsEnabled=true;}}
    private async Task Save()
    {
        if(!int.TryParse(_interval.Text,out int interval))throw new ArgumentException("Enter a whole number of minutes.");var draft=new BackupScheduleDraft(_enabled.IsChecked==true,interval,_directory,_revision);_fields.IsEnabled=false;
        try
        {
            if(draft.Enabled && (await CaptureEstimate()).HasKnownShortage)throw new ArgumentException("The estimated archive work exceeds available space. Free space or choose another folder.");
            if(!_loaded)throw new OperationCanceledException("The schedule view closed.");var saved=new BackupSchedules(store).Save(owner,draft);_revision=saved.Revision;
            _status.Text=saved.Enabled?"Automatic archive schedule saved. The app checks pending work every 15 seconds.":"Automatic archive schedule disabled. Existing archives are retained.";Refresh();
        }
        finally{_fields.IsEnabled=true;}
    }
}
