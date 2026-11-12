using System.Text;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;
using global::Google.Apis.Auth.OAuth2.Responses;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleConnectionsTests
{
    internal static TokenResponse Token()=>new(){AccessToken="Fictional-TEST-access-token",RefreshToken="Fictional-TEST-refresh-token",TokenType="Bearer",Scope=GoogleOAuthProtocol.DriveFileScope,ExpiresInSeconds=3600};
    internal static GoogleAccountDetails Account()=>new(new("fictional-permission-id","fictional@example.invalid","Fictional account"),10000,1000,DateTimeOffset.UtcNow.ToString("O"));
    internal static GoogleConnections Service(BusinessStore store,string root)=>new(store,root){Authenticate=(_,_)=>Task.FromResult(Token()),VerifyAccount=(_,_)=>Task.FromResult(Account())};
    internal static GoogleClientConfiguration Configure(LedgerFixture f,TestStore files)=>new GoogleClientSettings(f.Business.Store,files.Folder).Save(f.Business.Owner,GoogleClientSettingsTests.Client());
    private static long Watermark(LedgerFixture f)=>(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
    [Fact]
    public async Task VerifiedConnectionReopensProtectedAndBackgroundLoadingDoesNotUnlockTheOwner()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var configuration=Configure(f,files);var connections=Service(f.Business.Store,files.Folder);long watermark=Watermark(f);
        Assert.Equal("Not connected",connections.Current(f.Business.Owner).Status);var saved=await connections.ConnectAsync(f.Business.Owner,configuration.Id);Assert.Equal("fictional-permission-id",saved.Account.Account.PermissionId);Assert.Equal(watermark+1,Watermark(f));Assert.Equal("Connected",connections.Current(f.Business.Owner).Status);
        var reopened=Service(new BusinessStore(f.Business.Files.DatabasePath),files.Folder);Assert.Equal(saved,reopened.Load()!.Connection);Assert.Equal("Fictional-TEST-refresh-token",reopened.Load()!.Token.RefreshToken);
        using(var captured=new ArchiveSources(f.Business.Store).Capture(f.Business.Owner)){Assert.Single(captured.Manifest.Files);Assert.Equal(-1,File.ReadAllBytes(captured.FilePath("sarahbeauty.db")).AsSpan().IndexOf(Encoding.UTF8.GetBytes("Fictional-TEST-refresh-token")));}
        f.Business.Owner.Lock();var expires=f.Business.Owner.ExpiresAt;var background=reopened.Load();Assert.NotNull(background);Assert.Equal(expires,f.Business.Owner.ExpiresAt);Assert.DoesNotContain("Fictional",background.ToString());Assert.Throws<UnauthorizedAccessException>(()=>reopened.Current(f.Business.Owner));Assert.Equal(watermark+1,Watermark(f));
    }
    [Fact]
    public async Task ForeignLockedAndUnconfiguredRequestsCannotBeginConsent()
    {
        using var f=new LedgerFixture();using var foreign=new BusinessFixture();using var files=new TestStore();var connections=Service(f.Business.Store,files.Folder);int calls=0;connections.Authenticate=(_,_)=>{calls++;return Task.FromResult(Token());};
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>connections.ConnectAsync(foreign.Owner,Guid.NewGuid().ToString()));await Assert.ThrowsAsync<ArgumentException>(()=>connections.ConnectAsync(f.Business.Owner,Guid.NewGuid().ToString()));Assert.Equal(0,calls);
        var configuration=Configure(f,files);f.Business.Owner.Lock();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>connections.ConnectAsync(f.Business.Owner,configuration.Id));Assert.Equal(0,calls);Assert.Null(connections.Load());
    }
    [Fact]
    public async Task OwnerLockDuringConsentCannotPublishAnAuthorizedGrant()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var configuration=Configure(f,files);var connections=Service(f.Business.Store,files.Folder);long watermark=Watermark(f);
        connections.Authenticate=(_,_)=>{f.Business.Owner.Lock();return Task.FromResult(Token());};await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>connections.ConnectAsync(f.Business.Owner,configuration.Id));Assert.Null(connections.Load());Assert.Equal(watermark,Watermark(f));Assert.Null(new DeviceCredentialFiles(f.Business.Store,files.Folder).Read(DeviceCredentialKind.GoogleToken));
    }
    [Fact]
    public async Task ChangedConfigurationDuringConsentCannotReceiveTheEarlierGrant()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var configuration=Configure(f,files);var connections=Service(f.Business.Store,files.Folder);
        connections.Authenticate=(_,_)=>{new GoogleClientSettings(f.Business.Store,files.Folder).Save(f.Business.Owner,GoogleClientSettingsTests.Client("changed"),configuration.Id);return Task.FromResult(Token());};
        await Assert.ThrowsAsync<ArgumentException>(()=>connections.ConnectAsync(f.Business.Owner,configuration.Id));Assert.Null(connections.Load());Assert.Null(new DeviceCredentialFiles(f.Business.Store,files.Folder).Read(DeviceCredentialKind.GoogleToken));
    }
    [Fact]
    public async Task FailedAccountVerificationAndLateCancellationLeaveNoSavedConnection()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var configuration=Configure(f,files);var connections=Service(f.Business.Store,files.Folder);long watermark=Watermark(f);
        connections.VerifyAccount=(_,_)=>throw new GoogleConnectionFailure("DriveAccessUnavailable","Fictional fixed failure");await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>connections.ConnectAsync(f.Business.Owner,configuration.Id));Assert.Null(connections.Load());Assert.Equal(watermark,Watermark(f));
        using var cancellation=new CancellationTokenSource();connections.VerifyAccount=(_,_)=>{cancellation.Cancel();return Task.FromResult(Account());};await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>connections.ConnectAsync(f.Business.Owner,configuration.Id,cancellation:cancellation.Token));Assert.Null(connections.Load());Assert.Equal(watermark,Watermark(f));
    }
    [Fact]
    public async Task InterruptedPublicationCannotMixAnEarlierConnectionRecordWithNewTokenBytes()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var configuration=Configure(f,files);var connections=Service(f.Business.Store,files.Folder);var first=await connections.ConnectAsync(f.Business.Owner,configuration.Id);long watermark=Watermark(f);
        connections.FailurePoint=point=>{if(point=="Protected file published")throw new IOException("Fictional private write failure");};var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>connections.ConnectAsync(f.Business.Owner,configuration.Id,first.Id));Assert.DoesNotContain("Fictional",error.ToString());Assert.Equal(watermark,Watermark(f));
        Assert.Equal("GrantBindingMismatch",Assert.Throws<GoogleConnectionFailure>(()=>connections.Load()).Code);Assert.Equal("Reconnect required",connections.Current(f.Business.Owner).Status);connections.FailurePoint=null;
        var repaired=await connections.ConnectAsync(f.Business.Owner,configuration.Id,first.Id);Assert.NotEqual(first.Id,repaired.Id);Assert.Equal(repaired,connections.Load()!.Connection);Assert.Equal(watermark+1,Watermark(f));
    }
    [Fact]
    public async Task ConcurrentConsentCompletionsPublishOnlyOneReviewedConnection()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var configuration=Configure(f,files);long watermark=Watermark(f);
        var results=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(async()=>
        {
            try{await Service(new BusinessStore(f.Business.Files.DatabasePath),files.Folder).ConnectAsync(f.Business.Owner,configuration.Id);return true;}catch(ArgumentException){return false;}
        })));
        Assert.Single(results,result=>result);Assert.Equal(watermark+1,Watermark(f));Assert.Equal("Connected",Service(f.Business.Store,files.Folder).Current(f.Business.Owner).Status);
    }
    [Fact]
    public async Task ChangedClientOwnerConsentAndInstancePreventBackgroundReuse()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var configuration=Configure(f,files);var connections=Service(f.Business.Store,files.Folder);await connections.ConnectAsync(f.Business.Owner,configuration.Id);
        new GoogleClientSettings(f.Business.Store,files.Folder).Save(f.Business.Owner,GoogleClientSettingsTests.Client("replacement"),configuration.Id);Assert.Equal("GoogleConsentUnavailable",Assert.Throws<GoogleConnectionFailure>(()=>connections.Load()).Code);Assert.Equal("Reconnect required",connections.Current(f.Business.Owner).Status);
        f.Business.Files.Execute($"UPDATE AppMetadata SET Value='{Guid.NewGuid():N}' WHERE Key='WritableInstanceId';");Assert.Throws<UnauthorizedAccessException>(()=>connections.Load());Assert.Null(Service(new BusinessStore(f.Business.Files.DatabasePath),files.Folder).Load());
    }
    [Fact]
    public async Task UnavailableProtectedGrantNeedsReviewAndCanRecoverWithoutChangingTheConnection()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var configuration=Configure(f,files);var connections=Service(f.Business.Store,files.Folder);var saved=await connections.ConnectAsync(f.Business.Owner,configuration.Id);
        string path=Path.Combine(new DeviceCredentialFiles(f.Business.Store,files.Folder).DirectoryPath,"GoogleToken.dat");
        using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None)){Assert.Equal("Needs review",connections.Current(f.Business.Owner).Status);}
        Assert.Equal("Connected",connections.Current(f.Business.Owner).Status);Assert.Equal(saved,connections.Load()!.Connection);
    }

    [Fact]
    public async Task DeactivatingTheConsentingOwnerPreventsBackgroundGrantLoading()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var configuration=Configure(f,files);var connections=Service(f.Business.Store,files.Folder);await connections.ConnectAsync(f.Business.Owner,configuration.Id);
        f.Business.Files.Execute($"UPDATE Owners SET Active=0 WHERE Id='{f.Business.Owner.OwnerId}';");Assert.Equal("GoogleConsentUnavailable",Assert.Throws<GoogleConnectionFailure>(()=>connections.Load()).Code);
    }
}
