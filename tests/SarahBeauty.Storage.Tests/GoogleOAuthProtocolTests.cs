using System.Net;
using System.Security.Cryptography;
using System.Text;
using SarahBeauty.Business.Google;
using global::Google.Apis.Auth.OAuth2;
using global::Google.Apis.Auth.OAuth2.Requests;
using global::Google.Apis.Auth.OAuth2.Responses;
using global::Google.Apis.Http;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleOAuthProtocolTests
{
    private static GoogleDesktopClient Client()=>new("123456789-fictional.apps.googleusercontent.com","Fictional-TEST-client-secret","fictional-test-project");
    private static Dictionary<string,string> Parameters(string text)=>text.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries).Select(pair=>pair.Split('=',2)).ToDictionary(pair=>Uri.UnescapeDataString(pair[0]),pair=>Uri.UnescapeDataString(pair[1].Replace('+',' ')),StringComparer.Ordinal);
    private sealed class Receiver(string? wrongState=null,string? error=null):ICodeReceiver
    {
        public string RedirectUri=>"http://127.0.0.1:12345/authorize/";
        internal Dictionary<string,string>? Request;
        public Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(AuthorizationCodeRequestUrl request,CancellationToken cancellation)
        {
            Request=Parameters(request.Build().Query);var values=new Dictionary<string,string>{{"state",wrongState??request.State}};values.Add(error is null?"code":"error",error??"Fictional-TEST-authorization-code");return Task.FromResult(new AuthorizationCodeResponseUrl(values));
        }
    }
    private sealed class Handler(Receiver receiver,string? error=null,string? scope=null):HttpMessageHandler
    {
        internal int Calls;
        internal string? BodyOverride;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            Calls++;Assert.Equal("https://oauth2.googleapis.com/token",request.RequestUri!.AbsoluteUri);Assert.Equal(HttpMethod.Post,request.Method);var values=Parameters(await request.Content!.ReadAsStringAsync(cancellation));
            Assert.Equal("Fictional-TEST-authorization-code",values["code"]);Assert.Equal("authorization_code",values["grant_type"]);Assert.Equal(receiver.RedirectUri,values["redirect_uri"]);
            string challenge=Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(values["code_verifier"]))).TrimEnd('=').Replace('+','-').Replace('/','_');Assert.Equal(receiver.Request!["code_challenge"],challenge);
            string body=error is null?"{\"access_token\":\"Fictional-TEST-access-token\",\"refresh_token\":\"Fictional-TEST-refresh-token\",\"expires_in\":3600,\"token_type\":\"Bearer\",\"scope\":\""+(scope??GoogleOAuthProtocol.DriveFileScope)+"\"}":"{\"error\":\""+error+"\",\"error_description\":\"Fictional private provider detail\"}";
            return new(error is null?HttpStatusCode.OK:HttpStatusCode.BadRequest){Content=new StringContent(BodyOverride??body,Encoding.UTF8,"application/json")};
        }
    }
    private sealed class Factory(HttpMessageHandler handler):IHttpClientFactory
    {
        public ConfigurableHttpClient CreateHttpClient(CreateHttpClientArgs args)=>new(new ConfigurableMessageHandler(handler));
    }
    [Fact]
    public async Task S256VerifierAndNarrowOfflineConsentReachOnlyThePinnedTokenEndpoint()
    {
        var receiver=new Receiver();var handler=new Handler(receiver);var token=await new GoogleOAuthProtocol(()=>receiver,new Factory(handler)).AuthorizeAsync(Client());
        Assert.Equal("Fictional-TEST-refresh-token",token.RefreshToken);Assert.Equal(GoogleOAuthProtocol.DriveFileScope,token.Scope);Assert.Equal(1,handler.Calls);
        Assert.Equal("S256",receiver.Request!["code_challenge_method"]);Assert.Equal("offline",receiver.Request["access_type"]);Assert.Equal("consent select_account",receiver.Request["prompt"]);Assert.Equal("false",receiver.Request["include_granted_scopes"]);Assert.Equal(GoogleOAuthProtocol.DriveFileScope,receiver.Request["scope"]);Assert.Equal(64,receiver.Request["state"].Length);
    }
    [Fact]
    public async Task WrongCallbackStateCannotReachTheTokenExchange()
    {
        var receiver=new Receiver("Fictional unrelated state");var handler=new Handler(receiver);var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleOAuthProtocol(()=>receiver,new Factory(handler)).AuthorizeAsync(Client()));
        Assert.Equal("CallbackRejected",error.Code);Assert.Equal(0,handler.Calls);Assert.Null(error.InnerException);Assert.DoesNotContain("Fictional",error.ToString());
    }
    [Theory]
    [InlineData("access_denied","ConsentDenied")]
    [InlineData("invalid_grant","GrantUnavailable")]
    [InlineData("invalid_client","ClientUnavailable")]
    [InlineData("Fictional-private-error","AuthorizationFailed")]
    public async Task ProviderErrorsReturnFixedCodesAndNoProviderDetails(string provider,string expected)
    {
        var receiver=new Receiver();var handler=new Handler(receiver,provider);var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleOAuthProtocol(()=>receiver,new Factory(handler)).AuthorizeAsync(Client()));
        Assert.Equal(expected,error.Code);Assert.DoesNotContain("Fictional",error.ToString());Assert.Null(error.InnerException);
    }
    [Fact]
    public async Task BroaderReturnedPermissionsAreNotAcceptedAsTheRequestedFileGrant()
    {
        var receiver=new Receiver();var handler=new Handler(receiver,scope:"https://www.googleapis.com/auth/drive");var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleOAuthProtocol(()=>receiver,new Factory(handler)).AuthorizeAsync(Client()));Assert.Equal("UnexpectedScope",error.Code);
    }
    private sealed class WaitingReceiver:ICodeReceiver
    {
        public string RedirectUri=>"http://127.0.0.1:12345/authorize/";
        public async Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(AuthorizationCodeRequestUrl request,CancellationToken cancellation){await Task.Delay(Timeout.Infinite,cancellation);throw new InvalidOperationException();}
    }
    [Fact]
    public async Task MissingOfflineRefreshMaterialIsNotAcceptedAsACompleteConnection()
    {
        var receiver=new Receiver();var handler=new Handler(receiver){BodyOverride="{\"access_token\":\"Fictional-TEST-access\",\"expires_in\":3600,\"token_type\":\"Bearer\"}"};
        var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleOAuthProtocol(()=>receiver,new Factory(handler)).AuthorizeAsync(Client()));Assert.Equal("IncompleteGrant",error.Code);Assert.DoesNotContain("Fictional",error.ToString());
    }
    [Fact]
    public async Task MalformedTokenResponsesDoNotExposeProviderContentInErrors()
    {
        var receiver=new Receiver();var handler=new Handler(receiver){BodyOverride="Fictional private malformed provider content"};
        var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleOAuthProtocol(()=>receiver,new Factory(handler)).AuthorizeAsync(Client()));Assert.DoesNotContain("Fictional",error.ToString());Assert.Null(error.InnerException);
    }
    [Fact]
    public async Task ConsentTimeoutAndExplicitCancellationRemainDifferentResults()
    {
        var timed=new GoogleOAuthProtocol(()=>new WaitingReceiver(),timeout:TimeSpan.FromMilliseconds(50));var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>timed.AuthorizeAsync(Client()));Assert.Equal("ConsentTimedOut",error.Code);
        using var cancellation=new CancellationTokenSource(50);await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new GoogleOAuthProtocol(()=>new WaitingReceiver()).AuthorizeAsync(Client(),cancellation.Token));
    }
    private sealed class LocalProbe:GoogleSystemBrowserReceiver
    {
        internal Task<HttpResponseMessage>? Reply;
        protected override bool OpenBrowser(string url)
        {
            var values=Parameters(new Uri(url).Query);Assert.StartsWith("http://127.0.0.1:",values["redirect_uri"]);Reply=Task.Run(async()=>{using var client=new HttpClient();return await client.GetAsync(values["redirect_uri"]+"?code=Fictional-TEST-local-code&state="+values["state"]);});return true;
        }
    }
    [Fact]
    public async Task ActualSdkLoopbackReceiverAcceptsMatchingStateWithoutOpeningGoogle()
    {
        var local=new LocalProbe();using var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var receiver=new GoogleCallbackReceiver(local);var request=new GoogleAuthorizationCodeRequestUrl(new Uri("https://accounts.google.com/o/oauth2/v2/auth")){ClientId=Client().ClientId,RedirectUri=receiver.RedirectUri,Scope=GoogleOAuthProtocol.DriveFileScope};
        var response=await receiver.ReceiveCodeAsync(request,cancellation.Token);Assert.Equal("Fictional-TEST-local-code",response.Code);using var reply=await local.Reply!;Assert.Equal(HttpStatusCode.OK,reply.StatusCode);Assert.DoesNotContain(response.Code,await reply.Content.ReadAsStringAsync(cancellation.Token));
    }
}
