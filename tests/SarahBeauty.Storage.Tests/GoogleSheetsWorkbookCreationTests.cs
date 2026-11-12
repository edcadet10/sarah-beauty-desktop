using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsWorkbookCreationTests
{
    private const string FileId="fictional-created-workbook",Token="fictional-test-access";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateResponseOrLostResponseReopensWithoutAnotherPost(bool loseResponse)
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);int creates=0;
        var handler=new Handler(request=>
        {
            if(request.Method==HttpMethod.Post){creates++;Assert.Equal("CreateUncertain",new GoogleSheetsCreationCheckpoints(store).Current(job)!.State);if(loseResponse)throw new HttpRequestException("Fictional lost create response");return Task.FromResult(Json(new JsonObject{["id"]=FileId}));}
            return Task.FromResult(Response(request,job,permit));
        });using var client=new HttpClient(handler);
        var creation=new GoogleSheetsWorkbookCreation(store,client);
        if(loseResponse)await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>creation.ResolveAsync(permit,job,Access));else Assert.Equal(FileId,(await creation.ResolveAsync(permit,job,Access)).FileId);
        Assert.Equal(loseResponse?"CreateUncertain":"FileKnown",new GoogleSheetsCreationCheckpoints(store).Current(job)!.State);
        var reopened=new BusinessStore(store.DatabasePath);f.Ledger.Business.Owner.Lock();long watermark=f.Watermark;
        var observed=await new GoogleSheetsWorkbookCreation(reopened,client).ResolveAsync(permit,job,Access);Assert.Equal(FileId,observed.FileId);Assert.Equal(1,creates);
        Assert.Equal(FileId,new GoogleSheetsCreationCheckpoints(reopened).Current(job)!.FileId);Assert.Equal(watermark,f.Watermark);Assert.Equal(DateTimeOffset.MinValue,f.Ledger.Business.Owner.ExpiresAt);
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("duplicates")]
    [InlineData("page")]
    public async Task UnconfirmedLookupNeverCreatesAnotherWorkbook(string result)
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);
        var checkpoints=new GoogleSheetsCreationCheckpoints(store);checkpoints.BeginCreate(permit,job,checkpoints.Prepare(permit,job));int creates=0;
        var handler=new Handler(request=>
        {
            if(request.Method==HttpMethod.Post){creates++;throw new InvalidOperationException("No create expected");}
            if(request.RequestUri!.AbsolutePath.EndsWith("/files",StringComparison.Ordinal))
            {
                var body=new JsonObject{["files"]=result=="duplicates"?new JsonArray(FileFacts(job,permit),FileFacts(job,permit)):new JsonArray()};if(result=="page")body["nextPageToken"]="fictional-private-page";return Task.FromResult(Json(body));
            }
            return Task.FromResult(Response(request,job,permit));
        });using var client=new HttpClient(handler);await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleSheetsWorkbookCreation(store,client).ResolveAsync(permit,job,Access));
        Assert.Equal(0,creates);Assert.Equal("CreateUncertain",checkpoints.Current(job)!.State);
    }

    [Fact]
    public async Task PermissionWithdrawnDuringCreateRetainsIdAndPreventsFollowingReads()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);int readsAfterCreate=0;bool created=false;
        var handler=new Handler(async request=>
        {
            if(request.Method==HttpMethod.Post){created=true;await f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,false,permit.Policy.Revision));return Json(new JsonObject{["id"]=FileId});}
            if(created)readsAfterCreate++;return Response(request,job,permit);
        });using var client=new HttpClient(handler);await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleSheetsWorkbookCreation(store,client).ResolveAsync(permit,job,Access));
        Assert.Equal(FileId,new GoogleSheetsCreationCheckpoints(store).Current(job)!.FileId);Assert.Equal(0,readsAfterCreate);
    }

    [Fact]
    public async Task SharingTheDestinationBlocksCreationAndChangedAccessBlocksSubsequentHttp()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var store=f.Ledger.Business.Store;var job=new GoogleSheetsExportJobs(store).Reserve(permit,artifact);int requests=0;
        var handler=new Handler(request=>{requests++;var facts=FolderFacts(job,permit);facts["shared"]=true;return Task.FromResult(Json(facts));});using var client=new HttpClient(handler);
        await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleSheetsWorkbookCreation(store,client).ResolveAsync(permit,job,Access));Assert.Equal(1,requests);Assert.Equal("Prepared",new GoogleSheetsCreationCheckpoints(store).Current(job)!.State);
        await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleSheetsWorkbookCreation(store,client).ResolveAsync(permit,job,async token=>{await f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,false,permit.Policy.Revision),token);return Token;}));Assert.Equal(1,requests);
    }

    private static Task<string> Access(CancellationToken cancellation){cancellation.ThrowIfCancellationRequested();return Task.FromResult(Token);}
    private static HttpResponseMessage Response(HttpRequestMessage request,GoogleSheetsExportJob job,GoogleOutputPermit permit)
    {
        if(request.RequestUri!.AbsolutePath.EndsWith("/"+job.FolderId,StringComparison.Ordinal))return Json(FolderFacts(job,permit));
        if(request.RequestUri.AbsolutePath.EndsWith("/files",StringComparison.Ordinal))return Json(new JsonObject{["files"]=new JsonArray(FileFacts(job,permit))});
        return Json(FileFacts(job,permit));
    }
    private static JsonObject PrivateFacts(string id,string mime,GoogleOutputPermit permit)
    {
        string account=permit.Connection.Account.Account.PermissionId;return new(){["id"]=id,["mimeType"]=mime,["ownedByMe"]=true,["shared"]=false,["trashed"]=false,
            ["owners"]=new JsonArray(new JsonObject{["permissionId"]=account}),["permissions"]=new JsonArray(new JsonObject{["id"]=account,["type"]="user",["role"]="owner"})};
    }
    internal static JsonObject FolderFacts(GoogleSheetsExportJob job,GoogleOutputPermit permit)
    {
        var facts=PrivateFacts(job.FolderId,"application/vnd.google-apps.folder",permit);facts["name"]="Fictional TEST folder";facts["capabilities"]=new JsonObject{["canAddChildren"]=true};
        facts["appProperties"]=new JsonObject{["sarahBeautyPurpose"]="private-output-v1",["sarahBeautyBinding"]=job.FolderBinding};return facts;
    }
    internal static JsonObject FileFacts(GoogleSheetsExportJob job,GoogleOutputPermit permit)
    {
        var facts=PrivateFacts(FileId,GoogleSheetsFileSpec.Mime,permit);facts["parents"]=new JsonArray(job.FolderId);facts["capabilities"]=new JsonObject{["canEdit"]=true};
        facts["appProperties"]=JsonSerializer.SerializeToNode(GoogleSheetsJobRecords.Spec(job).Metadata())!["appProperties"]!.DeepClone();return facts;
    }
    private static HttpResponseMessage Json(JsonNode body)=>new(HttpStatusCode.OK){Content=new StringContent(body.ToJsonString())};
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> respond):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();Assert.Equal(Token,request.Headers.Authorization?.Parameter);return respond(request);
        }
    }
}
