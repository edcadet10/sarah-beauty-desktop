using System.Security.Cryptography;
using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed class GoogleClientSettings
{
    private readonly BusinessStore _store;
    private readonly DeviceCredentialFiles _files;
    private readonly string _business;
    internal Action<string>? FailurePoint {get;set;}
    public GoogleClientSettings(BusinessStore store):this(store,new DeviceCredentialFiles(store)){}
    internal GoogleClientSettings(BusinessStore store,string root):this(store,new DeviceCredentialFiles(store,root)){}
    private GoogleClientSettings(BusinessStore store,DeviceCredentialFiles files)
    {
        _store=store;_files=files;_business=store.Read(db=>db.Read("SELECT Id FROM Business;",r=>r.GetString(0)).Single());_files.FailurePoint=point=>FailurePoint?.Invoke(point);
    }
    public GoogleClientConfiguration? Current(OwnerSession owner)=>_store.Read(db=>{_store.RequireSession(db,owner);return GoogleClientRecords.Read(db,_business,_store.InstanceId);});
    public GoogleClientConfiguration Save(OwnerSession owner,byte[] downloadedJson,string? expectedConfigurationId=null)
    {
        var prior=Current(owner);if(prior?.Id!=expectedConfigurationId)throw new ArgumentException("The Google client configuration changed. Reload it before saving.");
        if(downloadedJson.Length>GoogleDesktopClient.MaximumFileBytes)throw new ArgumentException("The Google Desktop client file exceeds 64 KiB.");byte[] input=downloadedJson.ToArray();byte[]? encoded=null;
        try
        {
            var client=GoogleDesktopClient.Parse(input);if(SameClient(prior,client))return _store.Read(db=>{_store.RequireSession(db,owner);GoogleClientRecords.RequireExpected(db,_business,_store.InstanceId,expectedConfigurationId);return prior!;});
            var saved=new GoogleClientConfiguration(1,Guid.NewGuid().ToString(),_business,_store.InstanceId,checked((prior?.Revision??0)+1),client.ProjectId,GoogleClientRecords.Fingerprint(client),owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));
            encoded=JsonSerializer.SerializeToUtf8Bytes(new GoogleClientPayload{ConfigurationId=saved.Id,DownloadedJson=input});
            _files.Write(DeviceCredentialKind.GoogleClient,encoded,db=>
            {
                _store.RequireSession(db,owner);GoogleClientRecords.RequireExpected(db,_business,_store.InstanceId,expectedConfigurationId);GoogleClientRecords.Save(db,saved);BusinessStore.Audit(db,owner,"Configured Google Desktop client",saved.Id);FailurePoint?.Invoke("Configuration recorded");
            });return saved;
        }
        catch(GoogleConnectionFailure){throw;}
        catch(Exception error) when(error is IOException or Microsoft.Data.Sqlite.SqliteException){throw new GoogleConnectionFailure("ConfigurationSaveUnconfirmed","The Google client configuration save could not be confirmed. Reload its state before trying again.");}
        finally{CryptographicOperations.ZeroMemory(input);if(encoded is not null)CryptographicOperations.ZeroMemory(encoded);}
    }
    internal ConfiguredGoogleClient? Load()
    {
        var record=_store.Read(db=>GoogleClientRecords.Read(db,_business,_store.InstanceId));if(record is null)return null;
        byte[] clear=_files.Read(DeviceCredentialKind.GoogleClient)??throw new GoogleConnectionFailure("ClientFileMissing","The recorded Google client file is unavailable. Import the Desktop client again.");GoogleClientPayload? payload=null;
        try
        {
            payload=JsonSerializer.Deserialize<GoogleClientPayload>(clear,new JsonSerializerOptions{MaxDepth=8})??throw Invalid();
            if(payload.Version!=1 || payload.ConfigurationId!=record.Id || payload.DownloadedJson is not{Length:>0 and <=GoogleDesktopClient.MaximumFileBytes})throw Invalid();var client=GoogleDesktopClient.Parse(payload.DownloadedJson);
            if(client.ProjectId!=record.ProjectId || GoogleClientRecords.Fingerprint(client)!=record.ClientFingerprint)throw Invalid();return new(record,client);
        }
        catch(JsonException){throw Invalid();}
        finally{CryptographicOperations.ZeroMemory(clear);if(payload?.DownloadedJson is not null)CryptographicOperations.ZeroMemory(payload.DownloadedJson);}
    }
    private bool SameClient(GoogleClientConfiguration? prior,GoogleDesktopClient client)
    {
        if(prior is null)return false;
        try{var loaded=Load();return loaded is not null && loaded.Client.ClientId==client.ClientId && loaded.Client.ClientSecret==client.ClientSecret && loaded.Client.ProjectId==client.ProjectId;}
        catch(Exception error) when(error is IOException or InvalidDataException){return false;}
    }
    private static GoogleConnectionFailure Invalid()=>new("ConfigurationNeedsReview","The Google client file does not match its recorded configuration. Reload and import the reviewed Desktop client again.");
}
