using System.Net;
using System.Text;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleAccountProbeTests
{
    private const string Token="Fictional-TEST-account-access";
    private const string Body="{\"user\":{\"permissionId\":\"fictional-permission-id\",\"emailAddress\":\"fictional@example.invalid\",\"displayName\":\"Fictional account\"},\"storageQuota\":{\"limit\":\"1000\",\"usage\":\"250\"}}";
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> response):HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation){Calls++;Assert.Equal("Bearer",request.Headers.Authorization?.Scheme);Assert.Equal(Token,request.Headers.Authorization?.Parameter);Assert.Equal("www.googleapis.com",request.RequestUri!.Host);Assert.DoesNotContain(Token,request.RequestUri.AbsoluteUri);return Task.FromResult(response(request));}
    }
    private static HttpResponseMessage Response(string body,HttpStatusCode status=HttpStatusCode.OK)=>new(status){Content=new StringContent(body,Encoding.UTF8,"application/json")};
    [Fact]
    public async Task NarrowAccountRequestUsesTheBearerHeaderAndProjectsOnlyReturnedIdentityAndCapacity()
    {
        var handler=new Handler(request=>{Assert.Equal(HttpMethod.Get,request.Method);Assert.Equal("/drive/v3/about",request.RequestUri!.AbsolutePath);Assert.Contains("storageQuota(limit,usage)",Uri.UnescapeDataString(request.RequestUri.Query));return Response(Body);});
        using var client=new HttpClient(handler);var details=await new GoogleAccountProbe(client).ReadAsync(Token);Assert.Equal("fictional-permission-id",details.Account.PermissionId);Assert.Equal(750,details.ObservedFreeBytes);Assert.Equal(1,handler.Calls);Assert.DoesNotContain("fictional@example",details.Account.ToString());
    }
    [Fact]
    public async Task MissingLimitsStayUnknownAndUsageOverLimitHasZeroObservedSpace()
    {
        using var unknown=new HttpClient(new Handler(_=>Response(Body.Replace("\"limit\":\"1000\",","",StringComparison.Ordinal))));var details=await new GoogleAccountProbe(unknown).ReadAsync(Token);Assert.Null(details.StorageLimitBytes);Assert.Null(details.ObservedFreeBytes);
        using var full=new HttpClient(new Handler(_=>Response(Body.Replace("\"250\"","\"1001\"",StringComparison.Ordinal))));Assert.Equal(0,(await new GoogleAccountProbe(full).ReadAsync(Token)).ObservedFreeBytes);
    }
    [Theory]
    [InlineData(401,"unknown","GrantUnavailable")]
    [InlineData(403,"storageQuotaExceeded","GoogleStorageFull")]
    [InlineData(403,"userRateLimitExceeded","GoogleQuotaLimited")]
    [InlineData(429,"unknown","GoogleQuotaLimited")]
    [InlineData(403,"accessNotConfigured","DriveAccessUnavailable")]
    [InlineData(503,"unknown","GoogleUnavailable")]
    [InlineData(302,"unknown","GoogleRequestRejected")]
    public async Task UnavailableGrantsQuotasStorageAndServerFailuresHaveFixedPrivateSafeErrors(int status,string reason,string expected)
    {
        using var client=new HttpClient(new Handler(_=>Response("{\"error\":{\"message\":\"Fictional private provider detail\",\"errors\":[{\"reason\":\""+reason+"\"}]}}",(HttpStatusCode)status)));
        var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleAccountProbe(client).ReadAsync(Token));Assert.Equal(expected,error.Code);Assert.DoesNotContain("Fictional",error.ToString());Assert.Null(error.InnerException);
    }
    [Theory]
    [InlineData("malformed")]
    [InlineData("missing")]
    [InlineData("negative")]
    [InlineData("overflow")]
    public async Task InvalidAccountOrStorageFactsCannotBecomeAUsableIdentity(string defect)
    {
        string body=defect switch{"malformed"=>"Fictional private invalid JSON","missing"=>"{\"user\":{}}","negative"=>Body.Replace("\"250\"","\"-1\"",StringComparison.Ordinal),"overflow"=>Body.Replace("\"250\"","\"9223372036854775808\"",StringComparison.Ordinal),_=>throw new ArgumentException()};
        using var client=new HttpClient(new Handler(_=>Response(body)));var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleAccountProbe(client).ReadAsync(Token));Assert.Equal("InvalidGoogleResponse",error.Code);Assert.DoesNotContain("Fictional",error.ToString());
    }
    private sealed class UnlengthenedStream(byte[] bytes):MemoryStream(bytes){public override bool CanSeek=>false;}
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OversizedResponsesAndMalformedBearerValuesAreRejected(bool knownLength)
    {
        var handler=new Handler(_=>knownLength?Response(new string('X',GoogleApiJson.MaximumResponseBytes+1)):new(HttpStatusCode.OK){Content=new StreamContent(new UnlengthenedStream(new byte[GoogleApiJson.MaximumResponseBytes+1]))});using var client=new HttpClient(handler);var probe=new GoogleAccountProbe(client);
        Assert.Equal("GoogleResponseTooLarge",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>probe.ReadAsync(Token))).Code);int prior=handler.Calls;
        Assert.Equal("IncompleteGrant",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>probe.ReadAsync("Fictional\r\nprivate value"))).Code);Assert.Equal(prior,handler.Calls);
        Assert.Equal("IncompleteGrant",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>probe.ReadAsync("Fictional\0private value"))).Code);Assert.Equal(prior,handler.Calls);
    }
    [Fact]
    public async Task BrokenResponseStreamsDoNotExposePrivateIoDiagnostics()
    {
        using var client=new HttpClient(new Handler(_=>throw new IOException("Fictional private response diagnostic")));var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleAccountProbe(client).ReadAsync(Token));Assert.Equal("GoogleUnavailable",error.Code);Assert.DoesNotContain("Fictional",error.ToString());Assert.Null(error.InnerException);
    }
    private sealed class WaitingHandler:HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation){await Task.Delay(Timeout.Infinite,cancellation);throw new InvalidOperationException();}
    }
    private sealed class WaitingBody:MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellation=default){await Task.Delay(Timeout.Infinite,cancellation);return 0;}
    }
    [Fact]
    public async Task ABodyThatStallsAfterHeadersStillHasACompleteRequestDeadline()
    {
        using var client=new HttpClient(new Handler(_=>new(HttpStatusCode.OK){Content=new StreamContent(new WaitingBody())})){Timeout=TimeSpan.FromMilliseconds(50)};
        using var safety=new CancellationTokenSource(5000);var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleAccountProbe(client).ReadAsync(Token,safety.Token));Assert.Equal("GoogleRequestTimedOut",error.Code);
    }
    [Fact]
    public async Task ExplicitCancellationIsPreservedAndHttpTimeoutHasAFixedResult()
    {
        using var timed=new HttpClient(new WaitingHandler()){Timeout=TimeSpan.FromMilliseconds(50)};Assert.Equal("GoogleRequestTimedOut",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleAccountProbe(timed).ReadAsync(Token))).Code);
        using var explicitClient=new HttpClient(new WaitingHandler());using var cancellation=new CancellationTokenSource(50);await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new GoogleAccountProbe(explicitClient).ReadAsync(Token,cancellation.Token));
    }
}
