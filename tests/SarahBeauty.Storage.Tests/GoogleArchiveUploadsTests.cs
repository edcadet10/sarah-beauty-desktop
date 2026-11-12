using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleArchiveUploadsTests
{
    private const string Token="Fictional-TEST-upload-access",Session="https://www.googleapis.com/upload/drive/v3/files?uploadType=resumable&upload_id=Fictional-TEST-private-session";
    private static byte[] Bytes(int count){byte[] bytes=new byte[count];for(int i=0;i<count;i++)bytes[i]=(byte)(i%251);Encoding.ASCII.GetBytes("age-encryption.org/v1\n").CopyTo(bytes,0);return bytes;}
    private static GoogleArchiveUploadSpec Spec(byte[] bytes)=>new("fictional-archive-id","fictional-folder-id",Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"),bytes.Length,Convert.ToHexString(SHA256.HashData(bytes)));
    private static Task<string> Access(CancellationToken cancellation){cancellation.ThrowIfCancellationRequested();return Task.FromResult(Token);}
    private static HttpResponseMessage Begin(string location=Session){var response=new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent([])};response.Headers.Location=new Uri(location);return response;}
    private static HttpResponseMessage Pending(string? range){var response=new HttpResponseMessage((HttpStatusCode)308){Content=new ByteArrayContent([])};if(range is not null)response.Headers.TryAddWithoutValidation("Range",range);return response;}
    private static JsonObject Completed(GoogleArchiveUploadSpec spec)=>new(){["id"]=spec.FileId,["size"]=spec.ByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture),["sha256Checksum"]=spec.Sha256.ToLowerInvariant()};
    private static HttpResponseMessage Json(JsonNode body)=>new(HttpStatusCode.OK){Content=new StringContent(body.ToJsonString())};
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> respond):HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();Calls++;Assert.Equal("www.googleapis.com",request.RequestUri!.Host);Assert.Equal("https",request.RequestUri.Scheme);Assert.Equal(Token,request.Headers.Authorization?.Parameter);Assert.DoesNotContain(Token,request.RequestUri.AbsoluteUri);return respond(request);
        }
    }
    [Fact]
    public async Task ChunkedUploadUsesSavedFileIdentityActualRangesAndVerifiedCompletion()
    {
        byte[] bytes=Bytes(GoogleArchiveUploads.ChunkBytes+29);var spec=Spec(bytes);using var received=new MemoryStream();int accessCalls=0;
        var handler=new Handler(async request=>
        {
            if(request.Method==HttpMethod.Post)
            {
                Assert.Contains("ignoreDefaultVisibility=true",request.RequestUri!.Query);Assert.Equal(spec.ByteLength.ToString(),request.Headers.GetValues("X-Upload-Content-Length").Single());using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());Assert.Equal(spec.FileId,json.RootElement.GetProperty("id").GetString());Assert.Equal(spec.FolderId,json.RootElement.GetProperty("parents")[0].GetString());Assert.DoesNotContain("client_secret",json.RootElement.ToString());return Begin();
            }
            Assert.Equal(HttpMethod.Put,request.Method);var range=request.Content!.Headers.ContentRange!;Assert.Equal(received.Length,range.From);byte[] chunk=await request.Content.ReadAsByteArrayAsync();Assert.InRange(chunk.Length,1,GoogleArchiveUploads.ChunkBytes);Assert.Equal(chunk.Length,range.To-range.From+1);received.Write(chunk);return received.Length==bytes.Length?Json(Completed(spec)):Pending("bytes=0-"+(received.Length-1));
        });using var client=new HttpClient(handler);using var source=new MemoryStream(bytes);var result=await new GoogleArchiveUploads(client).UploadAsync(source,spec,cancellation=>{accessCalls++;return Access(cancellation);});Assert.Equal(spec.FileId,result.FileId);Assert.Equal(spec.Sha256,result.Sha256);Assert.Equal(bytes,received.ToArray());Assert.Equal(3,handler.Calls);Assert.Equal(3,accessCalls);
    }
    [Fact]
    public async Task UncertainChunkQueriesAcknowledgedBytesBeforeResumingFromTheActualOffset()
    {
        byte[] bytes=Bytes(GoogleArchiveUploads.ChunkBytes+17);var spec=Spec(bytes);int writes=0,queries=0;long received=0;
        var handler=new Handler(async request=>
        {
            if(request.Method==HttpMethod.Post)return Begin();var range=request.Content!.Headers.ContentRange!;
            if(range.From is null){queries++;Assert.Equal(0,request.Content.Headers.ContentLength);received=256*1024;return Pending("bytes=0-"+(received-1));}
            writes++;if(writes==1)throw new HttpRequestException("Fictional private session diagnostic");Assert.Equal(received,range.From);byte[] chunk=await request.Content.ReadAsByteArrayAsync();Assert.Equal(bytes.AsSpan((int)received,chunk.Length).ToArray(),chunk);received+=chunk.Length;return received==bytes.Length?Json(Completed(spec)):Pending("bytes=0-"+(received-1));
        });using var client=new HttpClient(handler);using var source=new MemoryStream(bytes);Assert.Equal(spec.FileId,(await new GoogleArchiveUploads(client).UploadAsync(source,spec,Access)).FileId);Assert.Equal(1,queries);Assert.Equal(2,writes);
    }
    [Fact]
    public async Task LostFinalResponseCanBeConfirmedWithoutSendingTheArchiveAgain()
    {
        byte[] bytes=Bytes(400);var spec=Spec(bytes);int writes=0,queries=0;var handler=new Handler(request=>
        {
            if(request.Method==HttpMethod.Post)return Task.FromResult(Begin());if(request.Content!.Headers.ContentRange!.From is null){queries++;return Task.FromResult(Json(Completed(spec)));}writes++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable){Content=new StringContent("Fictional private provider diagnostic")});
        });using var client=new HttpClient(handler);using var source=new MemoryStream(bytes);Assert.Equal(spec.Sha256,(await new GoogleArchiveUploads(client).UploadAsync(source,spec,Access)).Sha256);Assert.Equal(1,writes);Assert.Equal(1,queries);
    }
    [Theory]
    [InlineData("http://www.googleapis.com/upload/drive/v3/files?upload_id=Fictional")]
    [InlineData("https://example.invalid/upload/drive/v3/files?upload_id=Fictional")]
    [InlineData("https://www.googleapis.com:444/upload/drive/v3/files?upload_id=Fictional")]
    [InlineData("https://www.googleapis.com/upload/drive/v3/files?upload_id=Fictional#private")]
    [InlineData("https://www.googleapis.com/other?upload_id=Fictional")]
    public async Task UntrustedSessionLocationsReceiveNoArchiveBytes(string location)
    {
        byte[] bytes=Bytes(400);var handler=new Handler(_=>Task.FromResult(Begin(location)));using var client=new HttpClient(handler);using var source=new MemoryStream(bytes);var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleArchiveUploads(client).UploadAsync(source,Spec(bytes),Access));Assert.Equal("GoogleUploadLocationRejected",error.Code);Assert.Equal(1,handler.Calls);Assert.DoesNotContain("Fictional",error.ToString());Assert.DoesNotContain("upload_id",error.ToString());
    }
    [Theory]
    [InlineData("ahead")]
    [InlineData("backward")]
    [InlineData("nonzero-start")]
    [InlineData("missing")]
    public async Task UnconfirmedOrNonprogressingAcknowledgmentsCannotBecomeSuccess(string defect)
    {
        byte[] bytes=Bytes(GoogleArchiveUploads.ChunkBytes+17);var spec=Spec(bytes);int writes=0;var handler=new Handler(request=>
        {
            if(request.Method==HttpMethod.Post)return Task.FromResult(Begin());writes++;string? range=defect switch{"ahead"=>"bytes=0-"+(bytes.Length-1),"backward"=>writes==1?"bytes=0-"+(GoogleArchiveUploads.ChunkBytes-1):"bytes=0-1","nonzero-start"=>"bytes=2-10",_=>null};return Task.FromResult(Pending(range));
        });using var client=new HttpClient(handler);using var source=new MemoryStream(bytes);Assert.Equal("GoogleArchiveUnconfirmed",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleArchiveUploads(client).UploadAsync(source,spec,Access))).Code);
    }
    [Theory]
    [InlineData("id")]
    [InlineData("size")]
    [InlineData("wrong-hash")]
    [InlineData("missing-hash")]
    public async Task CompletionRequiresExactFileIdentityLengthAndSha256(string defect)
    {
        byte[] bytes=Bytes(400);var spec=Spec(bytes);var body=Completed(spec);switch(defect){case "id":body["id"]="fictional-other";break;case "size":body["size"]="401";break;case "wrong-hash":body["sha256Checksum"]=new string('0',64);break;default:body.Remove("sha256Checksum");break;}
        using var client=new HttpClient(new Handler(request=>Task.FromResult(request.Method==HttpMethod.Post?Begin():Json(body))));using var source=new MemoryStream(bytes);Assert.Equal("GoogleArchiveUnconfirmed",(await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleArchiveUploads(client).UploadAsync(source,spec,Access))).Code);
    }
    [Fact]
    public async Task ChangedOrPlaintextSourcesFailBeforeNetworkAndCancellationStopsBeforeAChunk()
    {
        byte[] bytes=Bytes(400);var spec=Spec(bytes);var handler=new Handler(_=>Task.FromResult(Begin()));using var client=new HttpClient(handler);var uploads=new GoogleArchiveUploads(client);bytes[99]^=1;using(var changed=new MemoryStream(bytes))await Assert.ThrowsAsync<InvalidDataException>(()=>uploads.UploadAsync(changed,spec,Access));bytes[0]=0;using(var plain=new MemoryStream(bytes))await Assert.ThrowsAsync<InvalidDataException>(()=>uploads.UploadAsync(plain,Spec(bytes),Access));Assert.Equal(0,handler.Calls);
        bytes=Bytes(400);using var source=new MemoryStream(bytes);using var cancellation=new CancellationTokenSource();int calls=0;await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>uploads.UploadAsync(source,Spec(bytes),token=>{if(++calls==2)cancellation.Cancel();return Access(token);},cancellation.Token));Assert.Equal(1,handler.Calls);
    }
    [Fact]
    public async Task GeneratedFileIdentityIsBoundedAndPrivateSessionDisplayIsRedacted()
    {
        using var client=new HttpClient(new Handler(request=>{Assert.Equal(HttpMethod.Get,request.Method);Assert.Equal("/drive/v3/files/generateIds",request.RequestUri!.AbsolutePath);return Task.FromResult(Json(new JsonObject{["ids"]=new JsonArray("fictional-generated-id")}));}));Assert.Equal("fictional-generated-id",await new GoogleArchiveUploads(client).GenerateIdAsync(Token));using var response=Begin();Assert.DoesNotContain("Fictional",GoogleUploadSession.Read(response).ToString());
    }
}
