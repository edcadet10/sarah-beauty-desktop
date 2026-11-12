using SarahBeauty.Business.Google;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleReconnectStateTests
{
    private static long Watermark(LedgerFixture f)=>(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
    [Fact]
    public async Task RejectedRenewalPersistsReconnectStateWithoutLoopingOrUnlockingTheOwner()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var config=GoogleConnectionsTests.Configure(f,files);var connections=GoogleConnectionsTests.Service(f.Business.Store,files.Folder);var connected=await connections.ConnectAsync(f.Business.Owner,config.Id);GoogleTokenRefreshTests.Expire(f.Business.Store,files.Folder);long watermark=Watermark(f);int calls=0;
        connections.Renew=(_,_,_)=>{calls++;throw new GoogleConnectionFailure("GrantUnavailable","Fictional fixed grant rejection");};f.Business.Owner.Lock();var expires=f.Business.Owner.ExpiresAt;
        Assert.Equal("GrantUnavailable",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>connections.AccessAsync())).Code);Assert.Equal(expires,f.Business.Owner.ExpiresAt);Assert.Equal(watermark,Watermark(f));
        Assert.Equal("GrantUnavailable",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>connections.AccessAsync())).Code);Assert.Equal(1,calls);
        var reopened=GoogleConnectionsTests.Service(new BusinessStore(f.Business.Files.DatabasePath),files.Folder);var owner=new OwnerAccess(f.Business.Store).SignIn(f.Business.Owner.OwnerId,BusinessFixture.Phrase);Assert.Equal("Reconnect required",reopened.Current(owner).Status);Assert.Equal("GrantUnavailable",reopened.Current(owner).FailureCode);
        var restored=await reopened.ConnectAsync(owner,config.Id,connected.Id);Assert.NotEqual(connected.Id,restored.Id);Assert.Equal("Connected",reopened.Current(owner).Status);Assert.NotNull(await reopened.AccessAsync());
    }
    [Fact]
    public async Task OfflineRenewalKeepsConsentAndCanRetryWhenTheNetworkReturns()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var config=GoogleConnectionsTests.Configure(f,files);var connections=GoogleConnectionsTests.Service(f.Business.Store,files.Folder);await connections.ConnectAsync(f.Business.Owner,config.Id);GoogleTokenRefreshTests.Expire(f.Business.Store,files.Folder);long watermark=Watermark(f);
        connections.Renew=(_,_,_)=>throw new GoogleConnectionFailure("GoogleUnavailable","Fictional fixed offline failure");await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>connections.AccessAsync());Assert.Equal("Connected",connections.Current(f.Business.Owner).Status);
        connections.Renew=(_,_,_)=>Task.FromResult(GoogleConnectionsTests.Token());Assert.NotNull(await connections.AccessAsync());Assert.Equal(watermark,Watermark(f));
    }
    [Fact]
    public async Task ADelayedFailureFromAnEarlierConnectionCannotPoisonTheReplacement()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var config=GoogleConnectionsTests.Configure(f,files);var connections=GoogleConnectionsTests.Service(f.Business.Store,files.Folder);var first=await connections.ConnectAsync(f.Business.Owner,config.Id);var earlier=await connections.AccessAsync();
        var newer=await connections.ConnectAsync(f.Business.Owner,config.Id,first.Id);long watermark=Watermark(f);Assert.False(connections.ObserveFailure(earlier,new("GrantUnavailable","Fictional old request")));
        Assert.Equal("Connected",connections.Current(f.Business.Owner).Status);Assert.Equal(newer,connections.Load()!.Connection);Assert.Equal(watermark,Watermark(f));
    }
    [Fact]
    public async Task UnknownProviderDetailsAreNeverPersistedAsConnectionState()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var config=GoogleConnectionsTests.Configure(f,files);var connections=GoogleConnectionsTests.Service(f.Business.Store,files.Folder);await connections.ConnectAsync(f.Business.Owner,config.Id);var access=await connections.AccessAsync();
        Assert.False(connections.ObserveFailure(access,new("Fictional-private-provider-code","Fictional private provider detail")));Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM AppMetadata WHERE Value LIKE '%Fictional-private-provider%';"));Assert.Equal("Connected",connections.Current(f.Business.Owner).Status);
    }
    [Fact]
    public async Task LocalDisconnectRemainsAvailableAfterARecordedGrantRejection()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var config=GoogleConnectionsTests.Configure(f,files);var connections=GoogleConnectionsTests.Service(f.Business.Store,files.Folder);var connected=await connections.ConnectAsync(f.Business.Owner,config.Id);var access=await connections.AccessAsync();
        Assert.True(connections.ObserveFailure(access,new("GrantUnavailable","Fictional rejection")));Assert.Equal("Reconnect required",connections.Current(f.Business.Owner).Status);var disconnected=connections.DisconnectLocal(f.Business.Owner,connected.Id);Assert.True(disconnected.DeviceTokenCleared);Assert.Equal("Disconnected",connections.Current(f.Business.Owner).Status);Assert.Null(connections.Load());
    }
    [Fact]
    public async Task AConfigurationChangePreventsAnEarlierFailureFromBeingRecorded()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var config=GoogleConnectionsTests.Configure(f,files);var connections=GoogleConnectionsTests.Service(f.Business.Store,files.Folder);await connections.ConnectAsync(f.Business.Owner,config.Id);var access=await connections.AccessAsync();
        new GoogleClientSettings(f.Business.Store,files.Folder).Save(f.Business.Owner,GoogleClientSettingsTests.Client("new"),config.Id);Assert.False(connections.ObserveFailure(access,new("GrantUnavailable","Fictional old client")));Assert.Equal("GoogleConsentUnavailable",connections.Current(f.Business.Owner).FailureCode);
    }
}
