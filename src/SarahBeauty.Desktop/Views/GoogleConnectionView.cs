using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class GoogleConnectionView(BusinessStore store,OwnerSession owner)
{
    private readonly GoogleClientSettings _clients=new(store);
    private readonly GoogleConnections _connections=new(store);
    private readonly TextBlock _configuration=Form.Text(),_connection=Form.Text(),_status=Form.Text();
    private readonly ContentControl _fields=new();
    private GoogleClientConfiguration? _client;
    private GoogleConnectionHealth? _health;
    private CancellationTokenSource? _cancellation;
    private bool _loaded;
    internal UIElement Build()
    {
        var fields=Form.Panel();fields.Add(_configuration,_connection,
            Form.Action("Import Google Desktop client",_status,()=>Run(Import)),Form.Action("Connect Google account",_status,()=>Run(Connect)),
            Form.Action("Disconnect Google on this device",_status,()=>Run(Disconnect)),Form.Action("Refresh Google connection status",_status,()=>{Refresh();return Task.CompletedTask;}),
            Form.Action("Open Google account access settings",_status,()=>Open("https://myaccount.google.com/connections")),
            Form.Action("Open Google Desktop setup guide",_status,()=>Open("https://developers.google.com/identity/protocols/oauth2/native-app")));_fields.Content=fields;
        var panel=Form.Panel();panel.Add(Form.Text("Import this app's own Google Desktop client JSON, then choose the intended account in the browser. Use a project without billing enrollment. The connection permits app-created or selected files. Google passwords stay in the browser; device credentials stay private to this Windows profile."),
            Form.Text("Connecting does not upload records or mark Drive/Sheets delivery complete. Local disconnect stops this device's saved access. Manage Google's server grant through Google account access settings."),_fields,
            Form.Action("Cancel Google connection operation",_status,()=>{_cancellation?.Cancel();return Task.CompletedTask;}),_status);
        panel.Loaded+=(_,_)=>{_loaded=true;try{Refresh();}catch(Exception error){_status.Text=error.Message;}};panel.Unloaded+=(_,_)=>{_loaded=false;_cancellation?.Cancel();};
        return new Expander{Header="Google connection",Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch};
    }
    private void Refresh()
    {
        _client=_clients.Current(owner);_health=_connections.Current(owner);
        _configuration.Text=_client is null?"Google Desktop client: Not configured.":$"Google Desktop client: Project {_client.ProjectId} • Revision {_client.Revision} • Configured {_client.ConfiguredAtUtc}";
        var last=_health.LastConnection;_connection.Text=$"Google connection: {_health.Status}"+(_health.FailureCode is null?"":" • "+_health.FailureCode);
        if(last is not null)_connection.Text+=$"\nLast verified account: {last.Account.Account.EmailAddress} • {last.Account.Account.DisplayName}\nAccount observation: {last.Account.ObservedAtUtc} • Observed free storage: {(last.Account.ObservedFreeBytes is long free?free.ToString("N0")+" bytes":"Unknown")}\nConnected {last.ConnectedAtUtc}"+(last.DisconnectedAtUtc is null?"":" • Disconnected locally "+last.DisconnectedAtUtc);
    }
    private async Task Run(Func<CancellationToken,Task> action)
    {
        if(_cancellation is not null)throw new ArgumentException("Wait for the current Google connection operation.");_cancellation=new();_fields.IsEnabled=false;
        try{await action(_cancellation.Token);}
        catch(OperationCanceledException){if(_loaded)_status.Text="Google operation cancelled. Refresh the saved status before retrying.";}
        finally{_fields.IsEnabled=true;_cancellation.Dispose();_cancellation=null;}
    }
    private async Task Import(CancellationToken cancellation)
    {
        string? expected=_client?.Id;string? path=await RecoveryFilePicker.GoogleClient();if(!_loaded || path is null)return;cancellation.ThrowIfCancellationRequested();_status.Text="Checking and protecting the Google Desktop client…";
        var saved=await ((App)Application.Current).Operations.RunAsync(async token=>
        {
            byte[] bytes=await ReadClient(path,token);try{return await Task.Run(()=>_clients.Save(owner,bytes,expected),token);}finally{CryptographicOperations.ZeroMemory(bytes);}
        },cancellation);
        if(!_loaded)return;_client=saved;Refresh();_status.Text="Google Desktop client saved privately. Connect the intended Google account next.";
    }
    private async Task Connect(CancellationToken cancellation)
    {
        var client=_client??throw new ArgumentException("Import this app's Google Desktop client first.");string? prior=_health?.LastConnection?.Id;_status.Text="Complete consent in your browser, then return to Sarah Beauty. No records are uploaded by this step.";
        var saved=await ((App)Application.Current).Operations.RunAsync(token=>_connections.ConnectAsync(owner,client.Id,prior,token),cancellation);
        if(!_loaded)return;Refresh();_status.Text="Google account verified and saved: "+saved.Account.Account.EmailAddress+". Drive upload and Sheets export remain separate operations.";
    }
    private async Task Disconnect(CancellationToken cancellation)
    {
        string expected=_health?.LastConnection?.Id??throw new ArgumentException("There is no recorded Google connection on this device.");
        var result=await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(()=>_connections.DisconnectLocal(owner,expected),token),cancellation);
        if(!_loaded)return;Refresh();_status.Text=result.DeviceTokenCleared?"Google disconnected locally. Device token cleared; client settings and existing records/files are retained. Manage server access in Google account settings.":"Local disconnection was recorded. Protected token cleanup needs review; refresh the current connection before retrying.";
    }
    private static async Task<byte[]> ReadClient(string path,CancellationToken cancellation)
    {
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(file.Length is <1 or >65536)throw new ArgumentException("Choose a Google Desktop client JSON file no larger than 64 KiB.");byte[] bytes=new byte[checked((int)file.Length)];
        try{await file.ReadExactlyAsync(bytes,cancellation);return bytes;}catch{CryptographicOperations.ZeroMemory(bytes);throw;}
    }
    private static Task Open(string address){Process.Start(new ProcessStartInfo(address){UseShellExecute=true});return Task.CompletedTask;}
}
