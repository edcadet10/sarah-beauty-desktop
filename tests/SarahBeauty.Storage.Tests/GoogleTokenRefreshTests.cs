using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;
using global::Google.Apis.Auth.OAuth2.Responses;
using global::Google.Apis.Http;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleTokenRefreshTests
{
    internal static void Expire(BusinessStore store,string root)
    {
        var files=new DeviceCredentialFiles(store,root);byte[] clear=files.Read(DeviceCredentialKind.GoogleToken)!;
        try{var payload=JsonSerializer.Deserialize<GoogleGrantPayload>(clear)!;payload.ExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O");byte[] encoded=JsonSerializer.SerializeToUtf8Bytes(payload);try{files.Write(DeviceCredentialKind.GoogleToken,encoded);}finally{CryptographicOperations.ZeroMemory(encoded);}}
        finally{CryptographicOperations.ZeroMemory(clear);}
    }
    private static long Watermark(LedgerFixture f)=>(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
    private static TokenResponse Renewed()=>new(){AccessToken="Fictional-TEST-renewed-access",RefreshToken=null,TokenType="Bearer",Scope=GoogleOAuthProtocol.DriveFileScope,ExpiresInSeconds=3600};
    [Fact]
    public async Task ACurrentTokenNeedsNoNetworkAndDoesNotRenewLockedHumanAccess()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var config=GoogleConnectionsTests.Configure(f,files);var connections=GoogleConnectionsTests.Service(f.Business.Store,files.Folder);await connections.ConnectAsync(f.Business.Owner,config.Id);
        connections.Renew=(_,_,_)=>throw new InvalidOperationException("No refresh expected");f.Business.Owner.Lock();var expires=f.Business.Owner.ExpiresAt;long watermark=Watermark(f);var access=await connections.AccessAsync();connections.RequireAccess(access);
        Assert.Equal("Fictional-TEST-access-token",access.AccessToken);Assert.Equal(expires,f.Business.Owner.ExpiresAt);Assert.Equal(watermark,Watermark(f));Assert.DoesNotContain("Fictional",access.ToString());
    }
    [Fact]
    public async Task ExpiredTokensRenewProtectedWithOldRefreshMaterialAndNoBusinessAuditLoop()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var config=GoogleConnectionsTests.Configure(f,files);var connections=GoogleConnectionsTests.Service(f.Business.Store,files.Folder);var saved=await connections.ConnectAsync(f.Business.Owner,config.Id);Expire(f.Business.Store,files.Folder);long watermark=Watermark(f);int calls=0;
        connections.Renew=(_,token,_)=>{calls++;Assert.Equal("Fictional-TEST-refresh-token",token);return Task.FromResult(Renewed());};f.Business.Owner.Lock();var expires=f.Business.Owner.ExpiresAt;
        var access=await connections.AccessAsync();Assert.Equal("Fictional-TEST-renewed-access",access.AccessToken);Assert.Equal(1,calls);Assert.Equal(saved,connections.Load()!.Connection);Assert.Equal("Fictional-TEST-refresh-token",connections.Load()!.Token.RefreshToken);Assert.Equal(expires,f.Business.Owner.ExpiresAt);Assert.Equal(watermark,Watermark(f));
        Assert.Equal(access.AccessToken,(await connections.AccessAsync()).AccessToken);Assert.Equal(1,calls);Assert.Empty(Directory.GetFiles(new DeviceCredentialFiles(f.Business.Store,files.Folder).DirectoryPath,"*.partial"));
    }
    [Fact]
    public async Task ConcurrentExpiredTokenReadersCoalesceIntoOneRenewal()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var config=GoogleConnectionsTests.Configure(f,files);var first=GoogleConnectionsTests.Service(f.Business.Store,files.Folder);await first.ConnectAsync(f.Business.Owner,config.Id);Expire(f.Business.Store,files.Folder);int calls=0;
        var results=await Task.WhenAll(Enumerable.Range(0,3).Select(_=>Task.Run(async()=>
        {
            var service=GoogleConnectionsTests.Service(new BusinessStore(f.Business.Files.DatabasePath),files.Folder);service.Renew=async(_,_,cancellation)=>{Interlocked.Increment(ref calls);await Task.Delay(100,cancellation);return Renewed();};return await service.AccessAsync();
        })));
        Assert.Equal(1,calls);Assert.All(results,result=>Assert.Equal("Fictional-TEST-renewed-access",result.AccessToken));
    }
    [Theory]
    [InlineData("configuration")]
    [InlineData("disconnect")]
    public async Task ChangedConsentDuringRenewalCannotPublishOldAuthority(string change)
    {
        using var f=new LedgerFixture();using var files=new TestStore();var config=GoogleConnectionsTests.Configure(f,files);var connections=GoogleConnectionsTests.Service(f.Business.Store,files.Folder);var connected=await connections.ConnectAsync(f.Business.Owner,config.Id);Expire(f.Business.Store,files.Folder);
        connections.Renew=(_,_,_)=>{if(change=="configuration")new GoogleClientSettings(f.Business.Store,files.Folder).Save(f.Business.Owner,GoogleClientSettingsTests.Client("changed"),config.Id);else connections.DisconnectLocal(f.Business.Owner,connected.Id);return Task.FromResult(Renewed());};
        Assert.Equal("GoogleConsentUnavailable",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>connections.AccessAsync())).Code);
        if(change=="disconnect")Assert.Null(new DeviceCredentialFiles(f.Business.Store,files.Folder).Read(DeviceCredentialKind.GoogleToken));else Assert.Equal("Fictional-TEST-access-token",JsonSerializer.Deserialize<GoogleGrantPayload>(new DeviceCredentialFiles(f.Business.Store,files.Folder).Read(DeviceCredentialKind.GoogleToken)!)!.AccessToken);
    }
    [Fact]
    public async Task AnotherGoogleAccountCannotReplaceTheBoundGrant()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var config=GoogleConnectionsTests.Configure(f,files);var connections=GoogleConnectionsTests.Service(f.Business.Store,files.Folder);await connections.ConnectAsync(f.Business.Owner,config.Id);Expire(f.Business.Store,files.Folder);
        connections.Renew=(_,_,_)=>Task.FromResult(Renewed());connections.VerifyAccount=(_,_)=>Task.FromResult(GoogleConnectionsTests.Account() with{Account=new("fictional-other-account","other@example.invalid","Fictional other")});
        Assert.Equal("GoogleAccountChanged",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>connections.AccessAsync())).Code);Assert.Equal("Fictional-TEST-access-token",connections.Load()!.Token.AccessToken);
    }
    [Fact]
    public async Task CancellationReleasesTheLeaseAndPreservesEarlierTokenMaterial()
    {
        using var f=new LedgerFixture();using var files=new TestStore();var config=GoogleConnectionsTests.Configure(f,files);var connections=GoogleConnectionsTests.Service(f.Business.Store,files.Folder);await connections.ConnectAsync(f.Business.Owner,config.Id);Expire(f.Business.Store,files.Folder);using var cancellation=new CancellationTokenSource();
        connections.Renew=(_,_,_)=>{cancellation.Cancel();return Task.FromResult(Renewed());};await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>connections.AccessAsync(cancellation.Token));Assert.Equal("Fictional-TEST-access-token",connections.Load()!.Token.AccessToken);
        using var lease=new DeviceCredentialFiles(f.Business.Store,files.Folder).TryRefreshLease();Assert.NotNull(lease);
    }
    private sealed class Factory(HttpMessageHandler handler):IHttpClientFactory
    {
        public ConfigurableHttpClient CreateHttpClient(CreateHttpClientArgs args)=>new(new ConfigurableMessageHandler(handler));
    }
    private sealed class TokenHandler(string? error=null,string? scope=null):HttpMessageHandler
    {
        internal int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            Calls++;Assert.Equal("https://oauth2.googleapis.com/token",request.RequestUri!.AbsoluteUri);Assert.Equal(HttpMethod.Post,request.Method);string form=await request.Content!.ReadAsStringAsync(cancellation);Assert.Contains("grant_type=refresh_token",form);Assert.Contains("refresh_token=Fictional-TEST-refresh-token",form);Assert.DoesNotContain("Fictional",request.RequestUri.Query);
            string body=error is null?"{\"access_token\":\"Fictional-TEST-renewed-access\",\"expires_in\":3600,\"token_type\":\"Bearer\""+(scope is null?"":",\"scope\":\""+scope+"\"")+"}":"{\"error\":\""+error+"\",\"error_description\":\"Fictional private provider detail\"}";
            return new(error is null?HttpStatusCode.OK:HttpStatusCode.BadRequest){Content=new StringContent(body,Encoding.UTF8,"application/json")};
        }
    }
    [Fact]
    public async Task ActualSdkRefreshUsesThePinnedPostAndPreservesOmittedRefreshMaterialWithoutABrowser()
    {
        var handler=new TokenHandler();var protocol=new GoogleOAuthProtocol(receivers:()=>throw new InvalidOperationException("No browser expected"),clients:new Factory(handler));
        var token=await protocol.RefreshAsync(new("123456789-fictional.apps.googleusercontent.com","Fictional-TEST-secret","fictional-test"),"Fictional-TEST-refresh-token");Assert.Equal("Fictional-TEST-refresh-token",token.RefreshToken);Assert.Equal(GoogleOAuthProtocol.DriveFileScope,token.Scope);Assert.Equal(1,handler.Calls);
    }
    [Theory]
    [InlineData("invalid_grant",null,"GrantUnavailable")]
    [InlineData("invalid_client",null,"ClientUnavailable")]
    [InlineData(null,"https://www.googleapis.com/auth/drive","UnexpectedScope")]
    public async Task InvalidOrBroaderRenewalResultsReturnFixedPrivateSafeErrors(string? provider,string? scope,string expected)
    {
        var protocol=new GoogleOAuthProtocol(clients:new Factory(new TokenHandler(provider,scope)));var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>protocol.RefreshAsync(new("123456789-fictional.apps.googleusercontent.com","Fictional-TEST-secret","fictional-test"),"Fictional-TEST-refresh-token"));Assert.Equal(expected,error.Code);Assert.DoesNotContain("Fictional",error.ToString());Assert.Null(error.InnerException);
    }
}
