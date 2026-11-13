using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsPublicationTransportTests
{
    private const string FileId="fictional-workbook",Account="fictional-account",Token="fictional-publication-access";
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicationRereadsPrivateStateAndRetryDoesNotRepeatAppliedPatch(bool loseResponse)
    {
        var spec=GoogleSheetsFilesTests.Spec();bool verified=false;int patches=0,accesses=0;var handler=new Handler(async request=>
        {
            if(request.Method==HttpMethod.Patch)
            {
                patches++;using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());Assert.Single(body.RootElement.EnumerateObject());
                Assert.Equal("verified",Assert.Single(body.RootElement.GetProperty("appProperties").EnumerateObject()).Value.GetString());verified=true;
                if(loseResponse&&patches==1)throw new HttpRequestException("Fictional lost publication response");return Json(new JsonObject{["id"]=FileId});
            }
            var facts=GoogleSheetsFilesTests.Facts(spec);if(verified)facts["appProperties"]!["sarahBeautyExportState"]="verified";return Json(facts);
        });using var client=new HttpClient(handler);var transport=new GoogleSheetsPublicationTransport(client);Task<string> Access(CancellationToken token){token.ThrowIfCancellationRequested();accesses++;return Task.FromResult(Token);}
        if(loseResponse)await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>transport.PublishAsync(spec,FileId,Account,Access));else Assert.Equal("verified",(await transport.PublishAsync(spec,FileId,Account,Access)).State);
        Assert.Equal("verified",(await transport.PublishAsync(spec,FileId,Account,Access)).State);Assert.Equal(1,patches);Assert.Equal(handler.Calls,accesses);
    }

    [Theory]
    [InlineData("privacy before")]
    [InlineData("privacy after")]
    [InlineData("unchanged state")]
    [InlineData("wrong echo")]
    public async Task PrivacyAndIndependentStateFailuresPreventPublicationConfirmation(string defect)
    {
        var spec=GoogleSheetsFilesTests.Spec();int patches=0;var handler=new Handler(request=>
        {
            if(request.Method==HttpMethod.Patch){patches++;return Task.FromResult(Json(new JsonObject{["id"]=defect=="wrong echo"?"different-file":FileId}));}
            var facts=GoogleSheetsFilesTests.Facts(spec);if(patches>0&&defect!="unchanged state")facts["appProperties"]!["sarahBeautyExportState"]="verified";
            if(defect=="privacy before"||defect=="privacy after"&&patches>0)facts["shared"]=true;return Task.FromResult(Json(facts));
        });using var client=new HttpClient(handler);await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleSheetsPublicationTransport(client).PublishAsync(spec,FileId,Account,_=>Task.FromResult(Token)));
        Assert.Equal(defect=="privacy before"?0:1,patches);
    }
    private static HttpResponseMessage Json(JsonNode body)=>new(HttpStatusCode.OK){Content=new StringContent(body.ToJsonString())};
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> respond):HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();Calls++;Assert.Equal("https",request.RequestUri!.Scheme);Assert.Equal("www.googleapis.com",request.RequestUri.Host);
            Assert.Equal(Token,request.Headers.Authorization?.Parameter);Assert.DoesNotContain(Token,request.RequestUri.AbsoluteUri);return respond(request);
        }
    }
}
