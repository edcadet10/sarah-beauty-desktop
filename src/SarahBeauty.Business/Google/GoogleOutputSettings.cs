using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed class GoogleOutputSettings
{
    private readonly BusinessStore _store;
    private readonly GoogleConnections _connections;
    internal Func<GoogleAccessGrant,string,string,CancellationToken,Task<GoogleDriveFolder>> CreateFolder {get;set;}=static(grant,binding,mode,cancellation)=>new GoogleDriveFolders().CreateAsync(grant.AccessToken,grant.Connection.Account.Account.PermissionId,binding,mode,cancellation);
    internal Func<GoogleAccessGrant,GoogleOutputPolicy,CancellationToken,Task<GoogleDriveFolder>> VerifyFolder {get;set;}=static(grant,policy,cancellation)=>new GoogleDriveFolders().VerifyAsync(grant.AccessToken,policy.FolderId,grant.Connection.Account.Account.PermissionId,policy.FolderBinding,cancellation);
    public GoogleOutputSettings(BusinessStore store):this(store,new GoogleConnections(store)){}
    internal GoogleOutputSettings(BusinessStore store,GoogleConnections connections){_store=store;_connections=connections;}
    public GoogleOutputPolicy? Current(OwnerSession owner)=>_store.Read(db=>{_store.RequireSession(db,owner);return GoogleOutputRecords.Read(db,owner.BusinessId,_store.InstanceId);});
    public async Task<GoogleOutputPolicy> CreateFolderAsync(OwnerSession owner,string expectedConnectionId,int expectedRevision,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();_store.Read(db=>{_store.RequireSession(db,owner);GoogleOutputRecords.RequireRevision(GoogleOutputRecords.Read(db,owner.BusinessId,_store.InstanceId),expectedRevision);GoogleConnectionRecords.RequireExpected(db,owner.BusinessId,_store.InstanceId,expectedConnectionId);return true;});
        var grant=await _connections.AccessAsync(cancellation).ConfigureAwait(false);if(grant.Connection.Id!=expectedConnectionId)throw ChangedConnection();_connections.RequireAccess(grant);
        string mode=_store.Read(db=>(string)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!);string binding=Guid.NewGuid().ToString("N");
        GoogleDriveFolder folder;try{folder=await CreateFolder(grant,binding,mode,cancellation).ConfigureAwait(false);}catch(GoogleConnectionFailure error){_connections.ObserveFailure(grant,error);throw;}cancellation.ThrowIfCancellationRequested();
        return _store.Transaction(db=>
        {
            cancellation.ThrowIfCancellationRequested();_store.RequireSession(db,owner);GoogleConnectionRecords.RequireConsent(db,grant.Connection);var prior=GoogleOutputRecords.Read(db,owner.BusinessId,_store.InstanceId);GoogleOutputRecords.RequireRevision(prior,expectedRevision);
            var saved=new GoogleOutputPolicy(1,prior?.Id??Guid.NewGuid().ToString(),owner.BusinessId,_store.InstanceId,grant.Connection.Id,checked(expectedRevision+1),folder.Id,folder.Name,binding,false,false,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"),folder.ObservedAtUtc);
            GoogleOutputRecords.Save(db,saved);BusinessStore.Audit(db,owner,"Configured private Google output folder",saved.Id);return saved;
        });
    }
    public async Task<GoogleOutputPolicy> SaveAsync(OwnerSession owner,GoogleOutputDraft draft,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();var prior=_store.Read(db=>{_store.RequireSession(db,owner);var value=GoogleOutputRecords.Read(db,owner.BusinessId,_store.InstanceId)??throw new ArgumentException("Create and verify a private Google output folder first.");GoogleOutputRecords.RequireRevision(value,draft.ExpectedRevision);return value;});
        GoogleDriveFolder? verified=null;GoogleAccessGrant? grant=null;
        if(draft.DriveArchivesEnabled || draft.SheetsExportEnabled)
        {
            grant=await _connections.AccessAsync(cancellation).ConfigureAwait(false);if(grant.Connection.Id!=prior.ConnectionId)throw ChangedConnection();_connections.RequireAccess(grant);
            try{verified=await VerifyFolder(grant,prior,cancellation).ConfigureAwait(false);}catch(GoogleConnectionFailure error){_connections.ObserveFailure(grant,error);throw;}
        }
        cancellation.ThrowIfCancellationRequested();return _store.Transaction(db=>
        {
            cancellation.ThrowIfCancellationRequested();_store.RequireSession(db,owner);var actual=GoogleOutputRecords.Read(db,owner.BusinessId,_store.InstanceId);if(actual!=prior)throw new ArgumentException("Google output settings changed. Reload them before saving.");if(grant is not null)GoogleConnectionRecords.RequireConsent(db,grant.Connection);
            if(prior.DriveArchivesEnabled==draft.DriveArchivesEnabled && prior.SheetsExportEnabled==draft.SheetsExportEnabled)return prior;
            var saved=prior with{Revision=checked(prior.Revision+1),DriveArchivesEnabled=draft.DriveArchivesEnabled,SheetsExportEnabled=draft.SheetsExportEnabled,ConfirmedBy=owner.OwnerId,ConfirmedAtUtc=DateTimeOffset.UtcNow.ToString("O"),FolderVerifiedAtUtc=verified?.ObservedAtUtc??prior.FolderVerifiedAtUtc};
            GoogleOutputRecords.Save(db,saved);BusinessStore.Audit(db,owner,"Changed Google output permission",saved.Id);return saved;
        });
    }
    internal GoogleOutputPermit Authorize(GoogleOutputKind kind)=>_store.ReadSnapshot(db=>
    {
        if(!Enum.IsDefined(kind))throw new ArgumentOutOfRangeException(nameof(kind));string business=db.Read("SELECT Id FROM Business;",r=>r.GetString(0)).Single();var policy=GoogleOutputRecords.Read(db,business,_store.InstanceId)??throw Unavailable();
        var connection=GoogleConnectionRecords.Read(db,business,_store.InstanceId)??throw Unavailable();var permit=new GoogleOutputPermit(policy,connection,kind);Require(db,permit);return permit;
    });
    internal void Require(DbWork db,GoogleOutputPermit permit)
    {
        var policy=permit.Policy;if(policy.InstanceId!=_store.InstanceId || GoogleOutputRecords.Read(db,policy.BusinessId,_store.InstanceId)!=policy || policy.ConnectionId!=permit.Connection.Id || !Enum.IsDefined(permit.Kind) || !(permit.Kind==GoogleOutputKind.DriveArchive?policy.DriveArchivesEnabled:policy.SheetsExportEnabled) || db.Scalar("SELECT Id FROM Owners WHERE Id=$owner AND BusinessId=$business AND Active=1;",("$owner",policy.ConfirmedBy),("$business",policy.BusinessId)) is null)throw Unavailable();GoogleConnectionRecords.RequireConsent(db,permit.Connection);
    }
    private static GoogleConnectionFailure Unavailable()=>new("GoogleOutputPermissionUnavailable","This Google output is not authorized for the current instance, account, and settings. Review output permission.");
    private static GoogleConnectionFailure ChangedConnection()=>new("GoogleOutputConnectionChanged","The Google account connection changed. Create and review a folder for the current connection before enabling outputs.");
}
