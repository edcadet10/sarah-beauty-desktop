using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleDriveFoldersTests
{
    private const string Token="Fictional-TEST-drive-access",Account="fictional-account",Folder="fictional-folder",Binding="0181416a8f7246acacec7eb10ba3c3ec";
    private static JsonObject Facts()=>new(){["id"]=Folder,["name"]="Fictional private TEST folder",["mimeType"]="application/vnd.google-apps.folder",["ownedByMe"]=true,["shared"]=false,["trashed"]=false,["owners"]=new JsonArray(new JsonObject{["permissionId"]=Account}),["permissions"]=new JsonArray(new JsonObject{["id"]=Account,["type"]="user",["role"]="owner",["deleted"]=false}),["appProperties"]=new JsonObject{["sarahBeautyPurpose"]="private-output-v1",["sarahBeautyBinding"]=Binding},["capabilities"]=new JsonObject{["canAddChildren"]=true}};
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> response):HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            Calls++;Assert.Equal("Bearer",request.Headers.Authorization?.Scheme);Assert.Equal(Token,request.Headers.Authorization?.Parameter);Assert.Equal("https",request.RequestUri!.Scheme);Assert.Equal("www.googleapis.com",request.RequestUri.Host);Assert.DoesNotContain(Token,request.RequestUri.AbsoluteUri);return response(request);
        }
    }
    private static HttpResponseMessage Response(JsonNode body)=>new(HttpStatusCode.OK){Content=new StringContent(body.ToJsonString())};
    [Fact]
    public async Task CreationIgnoresDomainDefaultsSendsNoBusinessRecordsAndVerifiesActualPermissions()
    {
        var handler=new Handler(async request=>
        {
            if(request.Method==HttpMethod.Post)
            {
                Assert.Equal("/drive/v3/files",request.RequestUri!.AbsolutePath);Assert.Contains("ignoreDefaultVisibility=true",request.RequestUri.Query);using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());var root=body.RootElement;
                Assert.Equal(3,root.EnumerateObject().Count());Assert.Equal("Sarah Beauty TEST exports and archives",root.GetProperty("name").GetString());Assert.False(root.TryGetProperty("parents",out _));Assert.False(root.TryGetProperty("permissions",out _));Assert.Equal(Binding,root.GetProperty("appProperties").GetProperty("sarahBeautyBinding").GetString());return Response(new JsonObject{["id"]=Folder});
            }
            Assert.Equal(HttpMethod.Get,request.Method);Assert.Equal("/drive/v3/files/"+Folder,request.RequestUri!.AbsolutePath);Assert.Contains("permissions(id,type,role,deleted)",Uri.UnescapeDataString(request.RequestUri.Query));return Response(Facts());
        });using var client=new HttpClient(handler);var folder=await new GoogleDriveFolders(client).CreateAsync(Token,Account,Binding,"TEST");Assert.Equal(Folder,folder.Id);Assert.Equal(Binding,folder.Binding);Assert.Equal(2,handler.Calls);
    }
    [Theory]
    [InlineData("shared")]
    [InlineData("domain")]
    [InlineData("group")]
    [InlineData("other-owner")]
    [InlineData("shared-drive")]
    [InlineData("trashed")]
    [InlineData("binding")]
    [InlineData("read-only")]
    [InlineData("file")]
    public async Task UnsafeOrForeignFoldersCannotBecomeVerifiedDestinations(string defect)
    {
        var body=Facts();switch(defect)
        {
            case "shared":body["shared"]=true;break;
            case "domain":case "group":body["permissions"]!.AsArray().Add(new JsonObject{["id"]="fictional-extra",["type"]=defect,["role"]="reader"});break;
            case "other-owner":body["owners"]![0]!["permissionId"]="fictional-other";break;
            case "shared-drive":body["driveId"]="fictional-shared-drive";break;
            case "trashed":body["trashed"]=true;break;
            case "binding":body["appProperties"]!["sarahBeautyBinding"]=Guid.NewGuid().ToString("N");break;
            case "read-only":body["capabilities"]!["canAddChildren"]=false;break;
            case "file":body["mimeType"]="application/octet-stream";break;
        }
        using var client=new HttpClient(new Handler(_=>Task.FromResult(Response(body))));var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleDriveFolders(client).VerifyAsync(Token,Folder,Account,Binding));Assert.Equal("GoogleFolderNotPrivate",error.Code);Assert.DoesNotContain("fictional",error.ToString());
    }
    [Fact]
    public async Task MissingPermissionFactsAndPathInjectionFailBeforeAUsableDestinationExists()
    {
        var body=Facts();body.Remove("permissions");var handler=new Handler(_=>Task.FromResult(Response(body)));using var client=new HttpClient(handler);var folders=new GoogleDriveFolders(client);
        Assert.Equal("InvalidGoogleResponse",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>folders.VerifyAsync(Token,Folder,Account,Binding))).Code);
        Assert.Equal("InvalidGoogleFileIdentity",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>folders.VerifyAsync(Token,"fictional/../about?token=value",Account,Binding))).Code);
        Assert.Equal("IncompleteGrant",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>folders.VerifyAsync("Fictional\r\nprivate",Folder,Account,Binding))).Code);Assert.Equal(1,handler.Calls);
    }
}
