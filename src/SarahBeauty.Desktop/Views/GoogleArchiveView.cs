using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class GoogleArchiveView(BusinessStore store,OwnerSession owner,Func<string>? runtime)
{
    private readonly GoogleOutputSettings _settings=new(store);
    private readonly CheckBox _enabled=new(){Content="Enable delivery of completed encrypted local archives to Google Drive while the app is open"};
    private readonly TextBlock _folder=Form.Text(),_health=Form.Text(),_queue=Form.Text(),_runtime=Form.Text(),_status=Form.Text(),_detail=Form.Text();
    private readonly ContentControl _fields=new();
    private readonly SearchableRecords _source=new("Completed local archive to deliver"),_history=new("Google archive delivery");
    private readonly DispatcherTimer _timer=new(){Interval=TimeSpan.FromSeconds(2)};
    private GoogleOutputPolicy? _policy;
    private IReadOnlyList<GoogleArchiveDeliveryHistory> _deliveries=[];
    private CancellationTokenSource? _cancellation;
    private bool _loaded,_initialized;
    internal UIElement Build()
    {
        var fields=Form.Panel();fields.Add(_folder,Form.Action("Create private Google archive folder",_status,()=>Run(CreateFolder)),_enabled,
            Form.Action("Save Google archive delivery choice",_status,()=>Run(Save)),Form.Action("Reload saved Google archive choice",_status,()=>{Reload();return Task.CompletedTask;}),
            Form.Action("Refresh Google archive health and history",_status,()=>{Refresh();return Task.CompletedTask;}),_source.Build(),Form.Action("Deliver selected local archive now",_status,()=>Run(Deliver)),
            _history.Build(),Form.Action("Show Google archive delivery attempts",_status,()=>{Show();return Task.CompletedTask;}));_fields.Content=fields;
        var panel=Form.Panel();panel.Add(Form.Text("Create a private folder in the verified Google account, then explicitly enable delivery. Creating a new folder resets delivery to disabled. Existing Google files are retained."),
            Form.Text("The app checks completed local archives every 15 seconds, including when owner access is locked. Configure automatic local archives or create an encrypted archive now to cover new changes. Delivery stops when the app is closed or asleep. No Google sharing invitations or storage purchases are made."),_fields,_health,_queue,_runtime,
            Form.Action("Cancel Google archive operation",_status,()=>{_cancellation?.Cancel();return Task.CompletedTask;}),_status,_detail);
        panel.Loaded+=(_,_)=>{_loaded=true;try{if(!_initialized){Reload();_initialized=true;}else Refresh();}catch(Exception error){_status.Text=error.Message;}_timer.Start();};panel.Unloaded+=(_,_)=>{_loaded=false;_timer.Stop();_cancellation?.Cancel();};_timer.Tick+=(_,_)=>ShowRuntime();return new Expander{Header="Google Drive archives",Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch};
    }
    private void Reload()
    {
        _policy=_settings.Current(owner);_enabled.IsChecked=_policy?.DriveArchivesEnabled??false;_folder.Text=_policy is null?"Google archive folder: Not configured.":$"Google archive folder: {_policy.FolderName} • Revision {_policy.Revision}\nFolder ID {_policy.FolderId} • Last verified {_policy.FolderVerifiedAtUtc}";Refresh();
    }
    private void Refresh()
    {
        var health=new GoogleArchiveHealthService(store).Current(owner);_health.Text=$"Google archive: {health.Status} • Current business watermark {health.CurrentWatermark}\nLast verified coverage {health.ArchivedWatermark?.ToString()??"None"} • Completed {health.CompletedAtUtc??"Never"}"+(health.FailureCode is null?"":" • "+health.FailureCode);
        var state=new GoogleArchiveQueue(store).Current(owner);_queue.Text=state is null?"Google archive retry queue: Never observed.":$"Google archive retry queue: {state.Status} • Observed {state.ObservedWatermark} • Delivered {state.DeliveredWatermark?.ToString()??"None"}\nNext retry {state.NextAttemptAtUtc??"When eligible"} • Failures {state.FailureCount} • Code {state.FailureCode??"None"}";
        var profile=new RecoveryProfiles(store).Current(owner);string selected=_source.SelectedId;var sources=new LocalBackups(store).History(owner).Where(j=>j.InstanceId==store.InstanceId && j.Status=="Succeeded" && j.RecipientFingerprint==profile?.RecipientFingerprint);_source.Update(sources.Select(j=>(j.Id,$"{j.FinishedAtUtc} • Watermark {j.Result!.Manifest.Watermark} • {Path.GetFileName(j.Destination)}")),selected);
        selected=_history.SelectedId;_deliveries=new GoogleArchiveDeliveries(store).History(owner);_history.Update(_deliveries.Select(h=>(h.Delivery.Id,$"{h.Delivery.CreatedAtUtc} • Watermark {h.Delivery.ArchiveWatermark} • {h.Attempts.LastOrDefault()?.Status??"Pending"}")),selected);ShowRuntime();
    }
    private void ShowRuntime()=>_runtime.Text="Google archive worker: "+(runtime?.Invoke()??"Unavailable");
    private async Task Run(Func<CancellationToken,Task> action)
    {
        if(_cancellation is not null)throw new ArgumentException("Wait for the current Google archive operation.");_cancellation=new();_fields.IsEnabled=false;
        try{await action(_cancellation.Token);}catch(OperationCanceledException){if(_loaded)_status.Text="Google archive operation cancelled. Refresh saved health and history before retrying; an accepted remote file may remain.";}finally{_fields.IsEnabled=true;_cancellation.Dispose();_cancellation=null;}
    }
    private async Task CreateFolder(CancellationToken cancellation)
    {
        var health=new GoogleConnections(store).Current(owner);if(health.Status!="Connected" || health.LastConnection is null)throw new ArgumentException("Connect and verify your Google account first.");int revision=_policy?.Revision??0;_status.Text="Creating and checking a private folder for "+health.LastConnection.Account.Account.EmailAddress+"…";
        await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>_settings.CreateFolderAsync(owner,health.LastConnection.Id,revision,token),token),cancellation);if(!_loaded)return;Reload();_status.Text="Private folder verified. Archive delivery remains disabled until you save the enabled choice.";
    }
    private async Task Save(CancellationToken cancellation)
    {
        var policy=_policy??throw new ArgumentException("Create and verify a private Google archive folder first.");var draft=new GoogleOutputDraft(_enabled.IsChecked==true,policy.SheetsExportEnabled,policy.Revision);
        await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>_settings.SaveAsync(owner,draft,token),token),cancellation);if(!_loaded)return;Reload();_status.Text=draft.DriveArchivesEnabled?"Google archive delivery enabled for completed local archives while the app is open.":"Google archive delivery disabled. Existing local and Google archives are retained.";
    }
    private async Task Deliver(CancellationToken cancellation)
    {
        string id=_source.SelectedId;if(string.IsNullOrEmpty(id))throw new ArgumentException("Choose a completed local archive from this instance and current recovery material.");_status.Text="Delivering and verifying the selected encrypted archive…";
        var attempt=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>new GoogleArchiveDeliveryWorker(store).DeliverAsync(owner,id,token),token),cancellation);if(!_loaded)return;Refresh();_status.Text="Google archive verified and delivery recorded: "+attempt.Status+". This does not establish a restore test.";
    }
    private void Show()
    {
        var history=_deliveries.SingleOrDefault(h=>h.Delivery.Id==_history.SelectedId)??throw new ArgumentException("Choose a saved Google archive delivery.");var delivery=history.Delivery;_detail.Text=$"Source instance {delivery.InstanceId} • Archive watermark {delivery.ArchiveWatermark}\nDrive file {delivery.DriveFileId} • Folder {delivery.FolderId}\nBytes {delivery.ByteLength:N0} • SHA-256 {delivery.Sha256}\n"+string.Join("\n",history.Attempts.Select(a=>$"{a.StartedAtUtc} • {a.Status} • Policy revision {a.PolicyRevision} • Finished {a.FinishedAtUtc??"Unconfirmed"} • Code {a.FailureCode??"None"}"));
    }
}
