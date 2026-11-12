using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsSchemaTransportTests
{
    private const string Token="Fictional-TEST-schema-access",FileId="fictional-workbook",Account="fictional-account";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitializationIsPrivateRereadAndRetryDoesNotDuplicateAnAppliedUpdate(bool lostResponse)
    {
        using var f=new LedgerFixture();var schema=GoogleSheetsSchemaInitializationTests.Schema(f);var spec=Spec(schema);var state=GoogleSheetsSchemaInitializationTests.Fresh(777);int writes=0,accessCalls=0;
        var handler=new Handler(async request=>
        {
            if(request.RequestUri!.Host=="www.googleapis.com")return Json(Facts(spec));
            if(request.Method==HttpMethod.Get)return Json(state);
            writes++;using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());GoogleSheetsSchemaInitializationTests.Apply(state,body.RootElement.GetProperty("requests"));
            if(lostResponse&&writes==1)throw new HttpRequestException("Fictional private lost schema response");return Json(new JsonObject{["spreadsheetId"]=FileId});
        });using var client=new HttpClient(handler);var transport=new GoogleSheetsSchemaTransport(client);Task<string> Access(CancellationToken token){token.ThrowIfCancellationRequested();accessCalls++;return Task.FromResult(Token);}
        if(lostResponse)Assert.Equal("GoogleUnavailable",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>transport.EnsureAsync(schema,spec,FileId,Account,Access))).Code);
        else await transport.EnsureAsync(schema,spec,FileId,Account,Access);
        await transport.EnsureAsync(schema,spec,FileId,Account,Access);
        Assert.Equal(1,writes);Assert.Equal(handler.Calls,accessCalls);schema.Require(JsonSerializer.SerializeToElement(state),FileId);
    }

    [Theory]
    [InlineData("private before")]
    [InlineData("private after")]
    [InlineData("changed readback")]
    [InlineData("access withdrawn")]
    [InlineData("source")]
    public async Task PrivacySourcePermissionAndIndependentLayoutFailuresPreventConfirmation(string defect)
    {
        using var f=new LedgerFixture();var schema=GoogleSheetsSchemaInitializationTests.Schema(f);var spec=Spec(schema);var state=GoogleSheetsSchemaInitializationTests.Fresh(777);int files=0,writes=0,access=0;
        var handler=new Handler(async request=>
        {
            if(request.RequestUri!.Host=="www.googleapis.com")
            {
                files++;var facts=Facts(spec);if(defect=="private before"||defect=="private after"&&files==3)facts["shared"]=true;return Json(facts);
            }
            if(request.Method==HttpMethod.Get)return Json(state);
            writes++;using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());GoogleSheetsSchemaInitializationTests.Apply(state,body.RootElement.GetProperty("requests"));
            if(defect=="changed readback")state["sheets"]![0]!["properties"]!["title"]="Changed";return Json(new JsonObject{["spreadsheetId"]=FileId});
        });using var client=new HttpClient(handler);var transport=new GoogleSheetsSchemaTransport(client);
        Task<string> Access(CancellationToken token){access++;if(defect=="access withdrawn"&&access==4)throw new UnauthorizedAccessException("Fictional permission withdrawn");return Task.FromResult(Token);}
        if(defect=="source")spec=spec with{GenerationId=Guid.NewGuid().ToString("N")};
        if(defect=="access withdrawn")await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>transport.EnsureAsync(schema,spec,FileId,Account,Access));
        else await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>transport.EnsureAsync(schema,spec,FileId,Account,Access));
        if(defect is "private before" or "source" or "access withdrawn")Assert.Equal(0,writes);if(defect=="source")Assert.Equal(0,handler.Calls);
    }

    private static GoogleSheetsFileSpec Spec(GoogleSheetsWorkbookSchema schema)=>new("fictional-folder","00000000000000000000000000000001","00000000000000000000000000000002",schema.GenerationId,schema.ManifestSha256,"TEST");
    private static HttpResponseMessage Json(JsonNode value)=>new(HttpStatusCode.OK){Content=new StringContent(value.ToJsonString())};
    private static JsonObject Facts(GoogleSheetsFileSpec spec)=>new()
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
            cancellation.ThrowIfCancellationRequested();Calls++;Assert.Equal("https",request.RequestUri!.Scheme);Assert.Contains(request.RequestUri.Host,new[]{"www.googleapis.com","sheets.googleapis.com"});
            Assert.Equal(Token,request.Headers.Authorization?.Parameter);Assert.DoesNotContain(Token,request.RequestUri.AbsoluteUri);return respond(request);
        }
    }
}
