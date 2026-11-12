using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsFilesTests
{
    private const string Token="Fictional-TEST-workbook-access",Account="fictional-account",FileId="fictional-workbook";
    internal static GoogleSheetsFileSpec Spec()=>new("fictional-folder","00000000000000000000000000000001","00000000000000000000000000000002","00000000000000000000000000000003",new string('A',64),"TEST");
    private static Task<string> Access(CancellationToken cancellation){cancellation.ThrowIfCancellationRequested();return Task.FromResult(Token);}

    [Fact]
    public async Task EmptyCreationReturnsIdentityBeforeSeparatePrivateVerification()
    {
        var spec=Spec();int accessCalls=0;var handler=new Handler(async request=>
        {
            if(request.Method==HttpMethod.Post)
            {
                Assert.Contains("ignoreDefaultVisibility=true",request.RequestUri!.Query);using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Assert.Equal(4,json.RootElement.EnumerateObject().Count());Assert.False(json.RootElement.TryGetProperty("id",out _));Assert.False(json.RootElement.TryGetProperty("permissions",out _));
                Assert.Equal(GoogleSheetsFileSpec.Mime,json.RootElement.GetProperty("mimeType").GetString());Assert.Equal(spec.JobId,json.RootElement.GetProperty("appProperties").GetProperty("sarahBeautyExportJob").GetString());return Json(new JsonObject{["id"]=FileId});
            }
            Assert.Equal("/drive/v3/files/"+FileId,request.RequestUri!.AbsolutePath);return Json(Facts(spec));
        });using var client=new HttpClient(handler);var files=new GoogleSheetsFiles(client);
        Task<string> Current(CancellationToken token){accessCalls++;return Access(token);}
        Assert.Equal(FileId,await files.CreateEmptyAsync(spec,Current));Assert.Equal(1,handler.Calls);
        Assert.Equal("staging",(await files.VerifyAsync(spec,FileId,Account,Current)).State);Assert.Equal(2,handler.Calls);Assert.Equal(2,accessCalls);
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("one")]
    [InlineData("verified")]
    [InlineData("two")]
    [InlineData("page")]
    [InlineData("incomplete")]
    [InlineData("wrong generation")]
    public async Task LookupRequiresOneCompletePrivateJobMatchAndNeverCreatesAFile(string result)
    {
        var spec=Spec();var facts=Facts(spec);if(result=="verified")facts["appProperties"]!["sarahBeautyExportState"]="verified";
        if(result=="wrong generation")facts["appProperties"]!["sarahBeautyGeneration"]=Guid.NewGuid().ToString("N");
        var body=new JsonObject{["files"]=result is "zero" or "page" or "incomplete"?new JsonArray():new JsonArray(facts)};
        if(result=="two")body["files"]!.AsArray().Add(facts.DeepClone());if(result=="page")body["nextPageToken"]="Fictional private page";if(result=="incomplete")body["incompleteSearch"]=true;
        var handler=new Handler(request=>
        {
            Assert.Equal(HttpMethod.Get,request.Method);string query=Uri.UnescapeDataString(request.RequestUri!.Query);
            Assert.Contains(spec.JobId,query);Assert.Contains(spec.FolderId,query);Assert.Contains("pageSize=2",query);return Task.FromResult(Json(body));
        });using var client=new HttpClient(handler);var files=new GoogleSheetsFiles(client);
        if(result is "two" or "page" or "incomplete" or "wrong generation")await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>files.FindAsync(spec,Account,Access));
        else{var observed=await files.FindAsync(spec,Account,Access);if(result=="zero")Assert.Null(observed);else Assert.Equal(result=="verified"?"verified":"staging",observed!.State);}
        Assert.Equal(1,handler.Calls);
    }

    [Fact]
    public async Task UncertainCreateIsNotBlindlyRetriedAndCanBeLookedUpByTheJob()
    {
        var spec=Spec();int creates=0;var handler=new Handler(request=>
        {
            if(request.Method==HttpMethod.Post){creates++;throw new HttpRequestException("Fictional private failure after create");}
            return Task.FromResult(Json(new JsonObject{["files"]=new JsonArray(Facts(spec))}));
        });using var client=new HttpClient(handler);var files=new GoogleSheetsFiles(client);
        Assert.Equal("GoogleUnavailable",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>files.CreateEmptyAsync(spec,Access))).Code);
        Assert.Equal(FileId,(await files.FindAsync(spec,Account,Access))!.FileId);Assert.Equal(1,creates);Assert.Equal(2,handler.Calls);
    }

    [Fact]
    public async Task InvalidIdentityDeniedAccessAndPostAccessCancellationSendNoRequest()
    {
        var handler=new Handler(_=>throw new InvalidOperationException("No HTTP expected"));using var client=new HttpClient(handler);var files=new GoogleSheetsFiles(client);int accessCalls=0;
        Task<string> Current(CancellationToken token){accessCalls++;return Access(token);}
        await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>files.CreateEmptyAsync(Spec() with{FolderId="../outside"},Current));Assert.Equal(0,accessCalls);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>files.CreateEmptyAsync(Spec(),_=>Task.FromException<string>(new UnauthorizedAccessException("Fictional denied"))));
        using var cancel=new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>files.CreateEmptyAsync(Spec(),_=>{cancel.Cancel();return Task.FromResult(Token);},cancel.Token));
        Assert.Equal(0,handler.Calls);
    }

    private static HttpResponseMessage Json(JsonNode value)=>new(HttpStatusCode.OK){Content=new StringContent(value.ToJsonString())};
    internal static JsonObject Facts(GoogleSheetsFileSpec spec)=>new()
    {
        ["id"]=FileId,["mimeType"]=GoogleSheetsFileSpec.Mime,["ownedByMe"]=true,["shared"]=false,["trashed"]=false,
        ["owners"]=new JsonArray(new JsonObject{["permissionId"]=Account}),["permissions"]=new JsonArray(new JsonObject{["id"]=Account,["type"]="user",["role"]="owner",["deleted"]=false}),
        ["parents"]=new JsonArray(spec.FolderId),["capabilities"]=new JsonObject{["canEdit"]=true},["appProperties"]=JsonSerializer.SerializeToNode(spec.Metadata())!["appProperties"]!.DeepClone()
    };
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
