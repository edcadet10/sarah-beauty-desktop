using System.Security.Cryptography;
using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;
using global::Google.Apis.Auth.OAuth2.Responses;

namespace SarahBeauty.Business.Google;

public sealed partial class GoogleConnections
{
    private readonly BusinessStore _store;
    private readonly DeviceCredentialFiles _files;
    private readonly GoogleClientSettings _clients;
    private readonly string _business;
    internal Action<string>? FailurePoint {get;set;}
    internal Func<GoogleDesktopClient,CancellationToken,Task<TokenResponse>> Authenticate {get;set;}=static(client,cancellation)=>new GoogleOAuthProtocol().AuthorizeAsync(client,cancellation);
    internal Func<string,CancellationToken,Task<GoogleAccountDetails>> VerifyAccount {get;set;}=static(token,cancellation)=>new GoogleAccountProbe().ReadAsync(token,cancellation);
    public GoogleConnections(BusinessStore store):this(store,new DeviceCredentialFiles(store),new GoogleClientSettings(store)){}
    internal GoogleConnections(BusinessStore store,string root):this(store,new DeviceCredentialFiles(store,root),new GoogleClientSettings(store,root)){}
    private GoogleConnections(BusinessStore store,DeviceCredentialFiles files,GoogleClientSettings clients)
    {
        _store=store;_files=files;_clients=clients;_business=store.Read(db=>db.Read("SELECT Id FROM Business;",r=>r.GetString(0)).Single());_files.FailurePoint=point=>FailurePoint?.Invoke(point);
    }
    public GoogleConnectionHealth Current(OwnerSession owner)
    {
        var record=_store.Read(db=>{_store.RequireSession(db,owner);return GoogleConnectionRecords.Read(db,_business,_store.InstanceId);});if(record is null)return new("Not connected",null);if(record.Status=="Disconnected")return new("Disconnected",record);
        try{var loaded=Load();return loaded is null?new("Not connected",null):new("Connected",loaded.Connection);}
        catch(GoogleConnectionFailure error){return new("Reconnect required",record,error.Code);}
        catch(InvalidDataException){return new("Needs review",record,"DeviceCredentialUnavailable");}
        catch(IOException){return new("Needs review",record,"DeviceCredentialUnavailable");}
    }
    public async Task<GoogleConnectionRecord> ConnectAsync(OwnerSession owner,string expectedConfigurationId,string? expectedConnectionId=null,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();_store.Read(db=>{_store.RequireSession(db,owner);GoogleClientRecords.RequireExpected(db,_business,_store.InstanceId,expectedConfigurationId);GoogleConnectionRecords.RequireExpected(db,_business,_store.InstanceId,expectedConnectionId);return true;});
        var client=_clients.Load()??throw new GoogleConnectionFailure("ClientFileMissing","Import and review this app's Google Desktop client first.");
        if(client.Configuration.Id!=expectedConfigurationId)throw new ArgumentException("The Google client configuration changed. Reload it before connecting.");
        var token=await Authenticate(client.Client,cancellation).ConfigureAwait(false);var material=GoogleGrantPayload.Create(token,Guid.NewGuid().ToString(),client.Configuration.Id);
        var account=await VerifyAccount(material.AccessToken,cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();
        return SaveGrant(owner,client.Configuration.Id,expectedConnectionId,material,account,cancellation);
    }
    private GoogleConnectionRecord SaveGrant(OwnerSession owner,string expectedConfigurationId,string? expectedConnectionId,GoogleGrantPayload material,GoogleAccountDetails account,CancellationToken cancellation)
    {
        var saved=new GoogleConnectionRecord(1,material.ConnectionId,_business,_store.InstanceId,expectedConfigurationId,"Connected",owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"),account);byte[] encoded=JsonSerializer.SerializeToUtf8Bytes(material);
        try
        {
            _files.Write(DeviceCredentialKind.GoogleToken,encoded,db=>
            {
                cancellation.ThrowIfCancellationRequested();_store.RequireSession(db,owner);GoogleClientRecords.RequireExpected(db,_business,_store.InstanceId,expectedConfigurationId);GoogleConnectionRecords.RequireExpected(db,_business,_store.InstanceId,expectedConnectionId);
                GoogleConnectionRecords.Save(db,saved);BusinessStore.Audit(db,owner,"Connected Google account",saved.Id);FailurePoint?.Invoke("Connection recorded");
            });return saved;
        }
        catch(GoogleConnectionFailure){throw;}
        catch(Exception error) when(error is IOException or Microsoft.Data.Sqlite.SqliteException){throw new GoogleConnectionFailure("ConnectionSaveUnconfirmed","The Google connection save could not be confirmed. Reload its state before reconnecting.");}
        finally{CryptographicOperations.ZeroMemory(encoded);}
    }
    internal LoadedGoogleGrant? Load()
    {
        var record=_store.ReadSnapshot(db=>{var saved=GoogleConnectionRecords.Read(db,_business,_store.InstanceId);if(saved is null || saved.Status=="Disconnected")return null;GoogleConnectionRecords.RequireConsent(db,saved);return saved;});if(record is null)return null;
        var client=_clients.Load()??throw new GoogleConnectionFailure("ClientFileMissing","The configured Google client is unavailable. Reconnect Google.");
        if(client.Configuration.Id!=record.ConfigurationId)throw new GoogleConnectionFailure("GoogleConsentUnavailable","The Google client configuration changed. Reconnect Google.");
        byte[] clear=_files.Read(DeviceCredentialKind.GoogleToken)??throw new GoogleConnectionFailure("GrantFileMissing","The protected Google grant is unavailable. Reconnect Google.");
        try
        {
            var payload=JsonSerializer.Deserialize<GoogleGrantPayload>(clear,new JsonSerializerOptions{MaxDepth=8})??throw new GoogleConnectionFailure("GrantBindingMismatch","The protected Google grant is incomplete. Reconnect Google.");payload.Validate(record);
            _store.Read(db=>{GoogleConnectionRecords.RequireConsent(db,record);return true;});return new(client,record,payload);
        }
        catch(JsonException){throw new GoogleConnectionFailure("GrantBindingMismatch","The protected Google grant is incomplete. Reconnect Google.");}
        finally{CryptographicOperations.ZeroMemory(clear);}
    }
}
