using SarahBeauty.Business.Google;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleOutputSettingsTests
{
    internal sealed class Fixture:IDisposable
    {
        internal readonly LedgerFixture Ledger=new();internal readonly TestStore Files=new();internal GoogleConnections Connections {get;}internal GoogleOutputSettings Settings {get;}internal GoogleClientConfiguration Client {get;}
        internal Fixture()
        {
            Client=GoogleConnectionsTests.Configure(Ledger,Files);Connections=GoogleConnectionsTests.Service(Ledger.Business.Store,Files.Folder);Settings=new(Ledger.Business.Store,Connections)
            {
                CreateFolder=(_,binding,_,_)=>Task.FromResult(new GoogleDriveFolder("fictional-output-folder","Fictional private TEST folder",binding,DateTimeOffset.UtcNow.ToString("O"))),
                VerifyFolder=(_,policy,_)=>Task.FromResult(new GoogleDriveFolder(policy.FolderId,policy.FolderName,policy.FolderBinding,DateTimeOffset.UtcNow.ToString("O")))
            };
        }
        internal async Task<GoogleOutputPolicy> Create(){var connection=await Connections.ConnectAsync(Ledger.Business.Owner,Client.Id);return await Settings.CreateFolderAsync(Ledger.Business.Owner,connection.Id,0);}
        internal long Watermark=>(long)TestStore.Scalar(Ledger.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        public void Dispose(){Files.Dispose();Ledger.Dispose();}
    }
    [Fact]
    public async Task NewFolderStartsDisabledAndSeparateOutputChoicesPersistWithoutRenewingLockedAccess()
    {
        using var f=new Fixture();Assert.Null(f.Settings.Current(f.Ledger.Business.Owner));var created=await f.Create();Assert.False(created.DriveArchivesEnabled);Assert.False(created.SheetsExportEnabled);Assert.Throws<GoogleConnectionFailure>(()=>f.Settings.Authorize(GoogleOutputKind.SheetsExport));long watermark=f.Watermark;
        var enabled=await f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,true,created.Revision));Assert.Equal(2,enabled.Revision);Assert.Equal(watermark+1,f.Watermark);Assert.Equal(enabled,await f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,true,enabled.Revision)));Assert.Equal(watermark+1,f.Watermark);
        var reopened=new GoogleOutputSettings(new BusinessStore(f.Ledger.Business.Files.DatabasePath),f.Connections);Assert.Equal(enabled,reopened.Current(f.Ledger.Business.Owner));f.Ledger.Business.Owner.Lock();var expiry=f.Ledger.Business.Owner.ExpiresAt;Assert.Equal(enabled,reopened.Authorize(GoogleOutputKind.SheetsExport).Policy);Assert.Equal(expiry,f.Ledger.Business.Owner.ExpiresAt);Assert.Throws<GoogleConnectionFailure>(()=>reopened.Authorize(GoogleOutputKind.DriveArchive));Assert.Equal(watermark+1,f.Watermark);
    }
    [Fact]
    public async Task ForeignLockedAndStaleOwnersCannotCreateOrChangeOutputPermission()
    {
        using var f=new Fixture();using var foreign=new BusinessFixture();var created=await f.Create();int calls=0;f.Settings.CreateFolder=(_,_,_,_)=>{calls++;throw new Exception("Must not run");};
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Settings.CreateFolderAsync(foreign.Owner,created.ConnectionId,1));await Assert.ThrowsAsync<ArgumentException>(()=>f.Settings.CreateFolderAsync(f.Ledger.Business.Owner,created.ConnectionId,0));Assert.Equal(0,calls);
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Settings.SaveAsync(f.Ledger.Business.Owner,new(true,true,0)));f.Ledger.Business.Owner.Lock();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Settings.SaveAsync(f.Ledger.Business.Owner,new(true,true,1)));
    }
    [Fact]
    public async Task OwnerLockAndCancellationDuringVerificationCannotEnableOutputs()
    {
        using var f=new Fixture();var created=await f.Create();long watermark=f.Watermark;using var cancellation=new CancellationTokenSource();
        f.Settings.VerifyFolder=(_,policy,_)=>{cancellation.Cancel();return Task.FromResult(new GoogleDriveFolder(policy.FolderId,policy.FolderName,policy.FolderBinding,DateTimeOffset.UtcNow.ToString("O")));};await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>f.Settings.SaveAsync(f.Ledger.Business.Owner,new(true,true,1),cancellation.Token));Assert.Equal(created,f.Settings.Current(f.Ledger.Business.Owner));Assert.Equal(watermark,f.Watermark);
        f.Settings.VerifyFolder=(_,policy,_)=>{f.Ledger.Business.Owner.Lock();return Task.FromResult(new GoogleDriveFolder(policy.FolderId,policy.FolderName,policy.FolderBinding,DateTimeOffset.UtcNow.ToString("O")));};await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Settings.SaveAsync(f.Ledger.Business.Owner,new(true,true,1)));Assert.Equal(watermark,f.Watermark);
    }
    [Fact]
    public async Task OfflineDisableNeedsNoGrantAndInvalidatesEarlierPermits()
    {
        using var f=new Fixture();var created=await f.Create();var enabled=await f.Settings.SaveAsync(f.Ledger.Business.Owner,new(true,true,1));var permit=f.Settings.Authorize(GoogleOutputKind.DriveArchive);f.Connections.DisconnectLocal(f.Ledger.Business.Owner,created.ConnectionId);
        f.Settings.VerifyFolder=(_,_,_)=>throw new IOException("Fictional offline");var disabled=await f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,false,enabled.Revision));Assert.False(disabled.DriveArchivesEnabled);Assert.False(disabled.SheetsExportEnabled);Assert.Throws<GoogleConnectionFailure>(()=>f.Ledger.Business.Store.Read(db=>{f.Settings.Require(db,permit);return true;}));
    }
    [Fact]
    public async Task ReplacementConnectionAndCopiedInstanceCannotReuseEarlierOutputConsent()
    {
        using var f=new Fixture();var created=await f.Create();await f.Settings.SaveAsync(f.Ledger.Business.Owner,new(true,true,1));var replacement=await f.Connections.ConnectAsync(f.Ledger.Business.Owner,f.Client.Id,created.ConnectionId);Assert.Throws<GoogleConnectionFailure>(()=>f.Settings.Authorize(GoogleOutputKind.SheetsExport));
        Assert.Equal("GoogleOutputConnectionChanged",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>f.Settings.SaveAsync(f.Ledger.Business.Owner,new(true,true,2)))).Code);var fresh=await f.Settings.CreateFolderAsync(f.Ledger.Business.Owner,replacement.Id,2);Assert.False(fresh.DriveArchivesEnabled);Assert.False(fresh.SheetsExportEnabled);Assert.NotEqual(created.FolderBinding,fresh.FolderBinding);
        f.Ledger.Business.Files.Execute($"UPDATE AppMetadata SET Value='{Guid.NewGuid():N}' WHERE Key='WritableInstanceId';");var reopened=new GoogleOutputSettings(new BusinessStore(f.Ledger.Business.Files.DatabasePath));Assert.Throws<GoogleConnectionFailure>(()=>reopened.Authorize(GoogleOutputKind.SheetsExport));
    }
    [Fact]
    public async Task ConnectionChangedDuringFolderCreationLeavesNoSavedDestinationOrOutputPermission()
    {
        using var f=new Fixture();var connected=await f.Connections.ConnectAsync(f.Ledger.Business.Owner,f.Client.Id);long watermark=f.Watermark;
        f.Settings.CreateFolder=(_,binding,_,_)=>{f.Connections.DisconnectLocal(f.Ledger.Business.Owner,connected.Id);return Task.FromResult(new GoogleDriveFolder("fictional-created-folder","Fictional empty folder",binding,DateTimeOffset.UtcNow.ToString("O")));};
        await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>f.Settings.CreateFolderAsync(f.Ledger.Business.Owner,connected.Id,0));Assert.Null(f.Settings.Current(f.Ledger.Business.Owner));Assert.Equal(watermark+1,f.Watermark);
    }
    [Fact]
    public async Task ConcurrentFolderReplacementAndRemoteGrantRejectionCannotEnableStaleOutputs()
    {
        using var f=new Fixture();var created=await f.Create();long watermark=f.Watermark;
        f.Settings.VerifyFolder=async(_,policy,cancellation)=>{await f.Settings.CreateFolderAsync(f.Ledger.Business.Owner,policy.ConnectionId,policy.Revision,cancellation);return new GoogleDriveFolder(policy.FolderId,policy.FolderName,policy.FolderBinding,DateTimeOffset.UtcNow.ToString("O"));};
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Settings.SaveAsync(f.Ledger.Business.Owner,new(true,true,1)));var replacement=f.Settings.Current(f.Ledger.Business.Owner)!;Assert.Equal(2,replacement.Revision);Assert.False(replacement.DriveArchivesEnabled);Assert.False(replacement.SheetsExportEnabled);Assert.Equal(watermark+1,f.Watermark);
        f.Settings.VerifyFolder=(_,_,_)=>throw new GoogleConnectionFailure("GrantUnavailable","Fictional fixed rejection");await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>f.Settings.SaveAsync(f.Ledger.Business.Owner,new(true,true,2)));Assert.Equal("Reconnect required",f.Connections.Current(f.Ledger.Business.Owner).Status);Assert.Equal(replacement,f.Settings.Current(f.Ledger.Business.Owner));Assert.Equal(watermark+1,f.Watermark);
    }
}
