using System.Net;
using System.Text.Json.Nodes;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleArchiveVerificationTests
{
    private const string Token="Fictional-TEST-verify-access",Account="fictional-account";
    private static GoogleArchiveUploadSpec Spec()=>new("fictional-file","fictional-folder",Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"),123,new string('A',64));
    private static JsonObject Facts(GoogleArchiveUploadSpec spec)=>new(){["id"]=spec.FileId,["mimeType"]="application/octet-stream",["ownedByMe"]=true,["shared"]=false,["trashed"]=false,["owners"]=new JsonArray(new JsonObject{["permissionId"]=Account}),["permissions"]=new JsonArray(new JsonObject{["id"]=Account,["type"]="user",["role"]="owner"}),["capabilities"]=new JsonObject{["canDownload"]=true},["parents"]=new JsonArray(spec.FolderId),["appProperties"]=new JsonObject{["sarahBeautyPurpose"]="encrypted-archive-v1",["sarahBeautyBinding"]=spec.FolderBinding,["sarahBeautyArchiveJob"]=spec.JobId,["sarahBeautyArchiveSha256"]=spec.Sha256},["size"]="123",["sha256Checksum"]=spec.Sha256.ToLowerInvariant()};
    private static Task<string> Access(CancellationToken cancellation){cancellation.ThrowIfCancellationRequested();return Task.FromResult(Token);}
    private sealed class Handler(Func<HttpResponseMessage> response):HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation){Calls++;Assert.Equal(HttpMethod.Get,request.Method);Assert.Equal("www.googleapis.com",request.RequestUri!.Host);Assert.Equal("/drive/v3/files/fictional-file",request.RequestUri.AbsolutePath);Assert.Equal(Token,request.Headers.Authorization?.Parameter);Assert.DoesNotContain(Token,request.RequestUri.AbsoluteUri);return Task.FromResult(response());}
    }
    [Fact]
    public async Task ExistingCompletionCanBeVerifiedWithoutAnotherContentWrite()
    {
        var spec=Spec();var handler=new Handler(()=>new(HttpStatusCode.OK){Content=new StringContent(Facts(spec).ToJsonString())});using var client=new HttpClient(handler);var result=await new GoogleArchiveUploads(client).FindVerifiedAsync(spec,Account,Access);Assert.Equal(new GoogleUploadedArchive(spec.FileId,123,spec.Sha256),result);Assert.Equal(1,handler.Calls);
    }
    [Theory]
    [InlineData("parent","GoogleArchiveUnconfirmed")]
    [InlineData("binding","GoogleArchiveUnconfirmed")]
    [InlineData("job","GoogleArchiveUnconfirmed")]
    [InlineData("hash","GoogleArchiveUnconfirmed")]
    [InlineData("group","GoogleArchiveNotPrivate")]
    [InlineData("owner","GoogleArchiveNotPrivate")]
    [InlineData("trashed","GoogleArchiveNotPrivate")]
    [InlineData("download","GoogleArchiveNotPrivate")]
    [InlineData("missing","InvalidGoogleResponse")]
    public async Task ChangedLocationBindingContentOrAccessCannotEstablishBackupSuccess(string defect,string code)
    {
        var spec=Spec();var body=Facts(spec);switch(defect)
        {
            case "parent":body["parents"]=new JsonArray("fictional-other-folder");break;
            case "binding":body["appProperties"]!["sarahBeautyBinding"]=Guid.NewGuid().ToString("N");break;
            case "job":body["appProperties"]!["sarahBeautyArchiveJob"]=Guid.NewGuid().ToString("N");break;
            case "hash":body["sha256Checksum"]=new string('B',64);break;
            case "group":body["permissions"]!.AsArray().Add(new JsonObject{["id"]="fictional-group",["type"]="group",["role"]="reader"});break;
            case "owner":body["ownedByMe"]=false;break;
            case "trashed":body["trashed"]=true;break;
            case "download":body["capabilities"]!["canDownload"]=false;break;
            default:body.Remove("permissions");break;
        }
        using var client=new HttpClient(new Handler(()=>new(HttpStatusCode.OK){Content=new StringContent(body.ToJsonString())}));var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleArchiveUploads(client).FindVerifiedAsync(spec,Account,Access));Assert.Equal(code,error.Code);Assert.DoesNotContain("fictional",error.ToString());
    }
    [Theory]
    [InlineData(404,"missing")]
    [InlineData(403,"DriveAccessUnavailable")]
    [InlineData(409,"GoogleFileAlreadyExists")]
    public async Task OnlyUnavailableFileResponsesReturnAbsentAndOtherFailuresRemainVisible(int status,string expected)
    {
        using var client=new HttpClient(new Handler(()=>new((HttpStatusCode)status){Content=new StringContent("{\"error\":{\"message\":\"Fictional private provider detail\"}}") }));var uploads=new GoogleArchiveUploads(client);
        if(status==404)Assert.Null(await uploads.FindVerifiedAsync(Spec(),Account,Access));else{var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>uploads.FindVerifiedAsync(Spec(),Account,Access));Assert.Equal(expected,error.Code);Assert.DoesNotContain("Fictional",error.ToString());}
    }
}
