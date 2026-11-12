using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;
using System.Collections.Concurrent;

namespace SarahBeauty.Business.Google;

public sealed class GoogleArchiveDeliveryWorker
{
    private static readonly ConcurrentDictionary<string,byte> ActiveAttempts=new();
    internal static bool IsActive(string attemptId)=>ActiveAttempts.ContainsKey(attemptId);
    private readonly BusinessStore _store;
    private readonly GoogleConnections _connections;
    private readonly GoogleOutputSettings _settings;
    private readonly GoogleArchiveDeliveries _deliveries;
    private readonly GoogleArchiveUploads _uploads;
    private readonly GoogleDriveFolders _folders;
    internal Action<string>? FailurePoint {get;set;}
    public GoogleArchiveDeliveryWorker(BusinessStore store):this(store,new(store),new(store),GoogleApiJson.Client){}
    internal GoogleArchiveDeliveryWorker(BusinessStore store,GoogleConnections connections,GoogleOutputSettings settings,HttpClient client){_store=store;_connections=connections;_settings=settings;_deliveries=new(store);_uploads=new(client);_folders=new(client);}
    public Task<GoogleArchiveAttempt> DeliverAsync(OwnerSession owner,string localArchiveJobId,CancellationToken cancellation=default)
    {
        _store.Read(db=>{_store.RequireSession(db,owner);return true;});return DeliverCurrentAsync(localArchiveJobId,cancellation);
    }
    internal async Task<GoogleArchiveAttempt> DeliverCurrentAsync(string localArchiveJobId,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();using var lease=GoogleArchiveDeliveryLease.TryAcquire(_store)??throw new GoogleConnectionFailure("GoogleArchiveBusy","Another archive delivery is active. Retry after it finishes.");var permit=_settings.Authorize(GoogleOutputKind.DriveArchive);GoogleArchiveAttempt? attempt=null;GoogleArchiveDelivery? active=null;
        async Task<string> Access(CancellationToken token)
        {
            Require(permit,active);var grant=await _connections.AccessAsync(token).ConfigureAwait(false);if(grant.Connection!=permit.Connection)throw new GoogleConnectionFailure("GoogleOutputConnectionChanged","The Google connection changed during archive delivery. Review the destination.");Require(permit,active);_connections.RequireAccess(grant);return grant.AccessToken;
        }
        async Task Folder()=>await _folders.VerifyAsync(await Access(cancellation).ConfigureAwait(false),permit.Policy.FolderId,permit.Connection.Account.Account.PermissionId,permit.Policy.FolderBinding,cancellation).ConfigureAwait(false);
        try
        {
            await Folder().ConfigureAwait(false);string id=await _uploads.GenerateIdAsync(await Access(cancellation).ConfigureAwait(false),cancellation).ConfigureAwait(false);var delivery=_deliveries.Reserve(permit,localArchiveJobId,id);active=delivery;_deliveries.InterruptRunning(delivery);attempt=_deliveries.Begin(permit,delivery);ActiveAttempts.TryAdd(attempt.Id,0);var spec=GoogleArchiveDeliveryRecords.Spec(delivery);
            async Task<GoogleUploadedArchive?> Verify()=>await _uploads.FindVerifiedAsync(spec,permit.Connection.Account.Account.PermissionId,Access,cancellation).ConfigureAwait(false);
            var remote=await Verify().ConfigureAwait(false);if(remote is null)
            {
                var source=_store.Read(db=>{_deliveries.Require(db,permit,delivery);return BackupJobRecords.Get(db,delivery.BusinessId,delivery.LocalArchiveJobId);});ArchivePaths.RejectLink(source.Destination);using var held=new FileStream(source.Destination,FileMode.Open,FileAccess.Read,FileShare.Read);
                try{await _uploads.UploadAsync(held,spec,Access,cancellation).ConfigureAwait(false);}catch(GoogleConnectionFailure error) when(error.Code=="GoogleFileAlreadyExists"){if(await Verify().ConfigureAwait(false) is null)throw GoogleArchiveUploadSpec.Unconfirmed();}
                FailurePoint?.Invoke("Archive content transfer completed");
                remote=await Verify().ConfigureAwait(false);
            }
            if(remote is null)throw GoogleArchiveUploadSpec.Unconfirmed();await Folder().ConfigureAwait(false);FailurePoint?.Invoke("Remote archive verified");cancellation.ThrowIfCancellationRequested();return _deliveries.Complete(permit,delivery,attempt,remote,cancellation);
        }
        catch(Exception error)
        {
            Exception failure=error;if(error is GoogleConnectionFailure google)try{_connections.ObserveFailure(permit.Connection,google);}catch(GoogleConnectionFailure observation){failure=observation;}
            if(attempt is not null)try{_deliveries.Fail(attempt,failure);}catch(Exception recordError) when(recordError is IOException or InvalidDataException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException){throw new GoogleConnectionFailure("GoogleDeliveryStateUnconfirmed","The archive attempt ended, but its local outcome could not be saved. Review delivery history and the reserved Google file before retrying.");}
            if(!ReferenceEquals(failure,error))throw failure;throw;
        }
        finally{if(attempt is not null)ActiveAttempts.TryRemove(attempt.Id,out _);}
    }
    private void Require(GoogleOutputPermit permit,GoogleArchiveDelivery? delivery)=>_store.Read(db=>{if(delivery is null)_settings.Require(db,permit);else _deliveries.Require(db,permit,delivery);return true;});
}
