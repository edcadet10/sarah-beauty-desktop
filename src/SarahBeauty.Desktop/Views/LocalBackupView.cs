using System.Security.Cryptography;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class LocalBackupView(BusinessStore store,OwnerSession owner,Action? retired=null,Func<string>? runtime=null)
{
    private readonly LocalBackups _backups=new(store);
    private readonly TextBlock _status=Form.Text(),_health=Form.Text(),_profile=Form.Text();
    private readonly SearchableRecords _history=new("Local archive attempt");
    private readonly ContentControl _detail=new();
    private readonly StackPanel _panel=Form.Panel();
    private IReadOnlyList<LocalBackupJob> _jobs=[];
    private CancellationTokenSource? _cancellation;
    internal UIElement Build()
    {
        _panel.Add(Form.Title("Backups and recovery"),Form.Text("Create a complete encrypted copy of the local database, issued PDFs, and managed evidence versions. Keep a copy on another device or private storage to protect against loss of this computer."),
            _profile,new RecoverySetupView(store,owner,Refresh).Build(),new BackupScheduleView(store,owner,runtime).Build(),new GoogleConnectionView(store,owner).Build(),Form.Action("Refresh backup health",_status,()=>{Refresh();return Task.CompletedTask;}),_health,
            Form.Action("Create encrypted archive now",_status,Create),Form.Action("Cancel local archive",_status,()=>{_cancellation?.Cancel();return Task.CompletedTask;}),_status,
            Form.Text("Google Drive upload: Never completed. Sheets export: Never completed. Second-device restore test: Never completed. Connection setup does not complete these delivery or restore checks."),
            _history.Build(),Form.Action("Show selected archive attempt",_status,Show),Form.Action("Verify selected encrypted file",_status,Verify),_detail);
        _panel.Loaded+=(_,_)=>{try{Refresh();}catch(Exception error){_status.Text=error.Message;}};_panel.Unloaded+=(_,_)=>_cancellation?.Cancel();return _panel;
    }
    private void Refresh()
    {
        var profile=new RecoveryProfiles(store).Current(owner);_profile.Text=profile is null?"Portable recovery: Not configured.":"Portable recovery confirmed "+profile.ConfirmedAtUtc+" • Recipient fingerprint "+profile.RecipientFingerprint;
        var health=_backups.Health(owner);_health.Text=HealthText(health);string selected=_history.SelectedId;_jobs=_backups.History(owner);
        _history.Update(_jobs.Select(j=>(j.Id,$"{j.StartedAtUtc} • {j.Status} • {Path.GetFileName(j.Destination)}")),selected);
    }
    internal static string HealthText(LocalBackupHealth health)=>$"Local archive: {health.Status} • Current business watermark {health.CurrentWatermark}\nLast completed coverage: {health.ArchivedWatermark?.ToString()??"None"} • Completed {health.CompletedAtUtc??"Never"}";
    private async Task Create()
    {
        if(_cancellation is not null)throw new ArgumentException("Wait for the current local archive.");if(new RecoveryProfiles(store).Current(owner) is null)throw new ArgumentException("Save and confirm portable recovery material first.");
        string? destination=await RecoveryFilePicker.Save("SarahBeauty-archive-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"),".age","Encrypted business archive");if(destination is null){_status.Text="Archive selection cancelled.";return;}
        _cancellation=new();_status.Text="Creating and verifying the encrypted archive…";
        try
        {
            var saved=await _backups.CreateAsync(owner,destination,_cancellation.Token);_status.Text=$"Encrypted archive saved • Captured watermark {saved.Result!.Manifest.Watermark} • {saved.Result.ByteLength} bytes • SHA-256 {saved.Result.Sha256}";
            try{Refresh();_history.Update(_jobs.Select(j=>(j.Id,$"{j.StartedAtUtc} • {j.Status} • {Path.GetFileName(j.Destination)}")),saved.Id);await Show();}catch(Exception){_status.Text+=". Refresh history to review this completed job.";}
        }
        catch(OperationCanceledException){_status.Text="Archive cancelled. No completed output was published.";TryRefresh();}
        catch{TryRefresh();throw;}
        finally{_cancellation.Dispose();_cancellation=null;}
    }
    private void TryRefresh(){try{Refresh();}catch(Exception){}}
    private LocalBackupJob Selected()=>_jobs.SingleOrDefault(j=>j.Id==_history.SelectedId)??throw new ArgumentException("Choose a local archive attempt.");
    private Task Show()
    {
        var selected=Selected();var panel=Form.Panel();panel.Add(LocalBackupDetailView.Build(selected));
        if(selected.Result is not null)panel.Add(new Expander{Header="Decrypt and review this recorded archive",Content=new RestoreReviewView(selected.Destination,selected.Result).Build(),HorizontalAlignment=HorizontalAlignment.Stretch});
        if(selected.Result is not null && retired is not null)panel.Add(new RetireStoreView(store,owner,selected,retired).Build());
        _detail.Content=panel;return Task.CompletedTask;
    }
    private async Task Verify()
    {
        var selected=Selected();var result=selected.Result??throw new ArgumentException("Choose a completed archive with recorded file facts.");
        using var input=new FileStream(selected.Destination,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(input.Length!=result.ByteLength || Convert.ToHexString(await SHA256.HashDataAsync(input))!=result.Sha256)throw new InvalidDataException("The selected encrypted file differs from its completed backup record. Preserve it and choose another known complete archive.");
        _status.Text="Encrypted file verified against the completed backup record. Decryption and second-device restore are separate checks.";
    }
}
