using SarahBeauty.Business.Google;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleLocalDisconnectTests
{
    private static long Watermark(LedgerFixture f)=>(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
    [Fact]
    public async Task LocalDisconnectInvalidatesConsentClearsOnlyTokenAndRecordsOneAudit()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var configuration=GoogleConnectionsTests.Configure(f,files);var connections=GoogleConnectionsTests.Service(f.Business.Store,files.Folder);var connected=await connections.ConnectAsync(f.Business.Owner,configuration.Id);long watermark=Watermark(f);
        var result=connections.DisconnectLocal(f.Business.Owner,connected.Id);Assert.True(result.DeviceTokenCleared);Assert.NotEqual(connected.Id,result.Record.Id);Assert.Equal(connected.Id,result.Record.PreviousConnectionId);Assert.Equal(connected.Account,result.Record.Account);Assert.Equal(f.Business.Owner.OwnerId,result.Record.DisconnectedBy);Assert.Equal(watermark+1,Watermark(f));
        Assert.Null(connections.Load());Assert.Equal("Disconnected",connections.Current(f.Business.Owner).Status);var privateStore=new DeviceCredentialFiles(f.Business.Store,files.Folder);Assert.Null(privateStore.Read(DeviceCredentialKind.GoogleToken));Assert.NotNull(privateStore.Read(DeviceCredentialKind.GoogleClient));
        Assert.Equal(result.Record,connections.DisconnectLocal(f.Business.Owner,result.Record.Id).Record);Assert.Equal(watermark+1,Watermark(f));
        var reopened=GoogleConnectionsTests.Service(new BusinessStore(f.Business.Files.DatabasePath),files.Folder);Assert.Equal("Disconnected",reopened.Current(f.Business.Owner).Status);var reconnected=await reopened.ConnectAsync(f.Business.Owner,configuration.Id,result.Record.Id);Assert.Equal("Connected",reopened.Current(f.Business.Owner).Status);Assert.NotEqual(connected.Id,reconnected.Id);
    }
    [Fact]
    public async Task ForeignLockedAndStaleRequestsCannotDisconnectOrDeleteTheGrant()
    {
        using var f=new LedgerFixture();using var foreign=new BusinessFixture();using var files=new TestStore();var configuration=GoogleConnectionsTests.Configure(f,files);var connections=GoogleConnectionsTests.Service(f.Business.Store,files.Folder);var connected=await connections.ConnectAsync(f.Business.Owner,configuration.Id);long watermark=Watermark(f);
        Assert.Throws<UnauthorizedAccessException>(()=>connections.DisconnectLocal(foreign.Owner,connected.Id));Assert.Throws<ArgumentException>(()=>connections.DisconnectLocal(f.Business.Owner,Guid.NewGuid().ToString()));Assert.Equal(watermark,Watermark(f));
        f.Business.Owner.Lock();Assert.Throws<UnauthorizedAccessException>(()=>connections.DisconnectLocal(f.Business.Owner,connected.Id));Assert.Equal(connected,connections.Load()!.Connection);
    }
    [Fact]
    public async Task UnavailableTokenCleanupDoesNotRestoreDisconnectedAuthority()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var configuration=GoogleConnectionsTests.Configure(f,files);var connections=GoogleConnectionsTests.Service(f.Business.Store,files.Folder);var connected=await connections.ConnectAsync(f.Business.Owner,configuration.Id);string path=Path.Combine(new DeviceCredentialFiles(f.Business.Store,files.Folder).DirectoryPath,"GoogleToken.dat");
        GoogleLocalDisconnectResult result;using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){result=connections.DisconnectLocal(f.Business.Owner,connected.Id);Assert.False(result.DeviceTokenCleared);Assert.Null(connections.Load());Assert.Equal("Disconnected",connections.Current(f.Business.Owner).Status);Assert.True(File.Exists(path));}
        Assert.True(connections.DisconnectLocal(f.Business.Owner,result.Record.Id).DeviceTokenCleared);Assert.False(File.Exists(path));
    }
    [Fact]
    public async Task InFlightConsentCannotUndoALaterLocalDisconnect()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var configuration=GoogleConnectionsTests.Configure(f,files);var connections=GoogleConnectionsTests.Service(f.Business.Store,files.Folder);var connected=await connections.ConnectAsync(f.Business.Owner,configuration.Id);
        var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var released=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending=GoogleConnectionsTests.Service(f.Business.Store,files.Folder);pending.Authenticate=async(_,cancellation)=>{started.SetResult();await released.Task.WaitAsync(cancellation);return GoogleConnectionsTests.Token();};
        var work=pending.ConnectAsync(f.Business.Owner,configuration.Id,connected.Id);await started.Task;var disconnected=connections.DisconnectLocal(f.Business.Owner,connected.Id);released.SetResult();await Assert.ThrowsAsync<ArgumentException>(()=>work);
        Assert.Equal(disconnected.Record,connections.Current(f.Business.Owner).LastConnection);Assert.Null(connections.Load());Assert.Null(new DeviceCredentialFiles(f.Business.Store,files.Folder).Read(DeviceCredentialKind.GoogleToken));
    }
    [Fact]
    public async Task DelayedCleanupCannotDeleteANewerExplicitlyConnectedGrant()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var configuration=GoogleConnectionsTests.Configure(f,files);var connections=GoogleConnectionsTests.Service(f.Business.Store,files.Folder);var connected=await connections.ConnectAsync(f.Business.Owner,configuration.Id);GoogleConnectionRecord? replacement=null;
        connections.FailurePoint=point=>
        {
            if(point!="Local disconnection recorded")return;var other=GoogleConnectionsTests.Service(new BusinessStore(f.Business.Files.DatabasePath),files.Folder);var disconnected=other.Current(f.Business.Owner).LastConnection!;replacement=other.ConnectAsync(f.Business.Owner,configuration.Id,disconnected.Id).GetAwaiter().GetResult();
        };
        var result=connections.DisconnectLocal(f.Business.Owner,connected.Id);Assert.False(result.DeviceTokenCleared);Assert.NotNull(replacement);Assert.Equal(replacement,connections.Load()!.Connection);Assert.NotNull(new DeviceCredentialFiles(f.Business.Store,files.Folder).Read(DeviceCredentialKind.GoogleToken));
    }
}
