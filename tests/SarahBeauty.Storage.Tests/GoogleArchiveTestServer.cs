using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

internal sealed class GoogleArchiveTestServer(GoogleOutputPolicy policy):HttpMessageHandler
{
    private readonly MemoryStream _received=new();
    private JsonObject? _metadata;
    internal int ContentWrites,Calls,Generated;
    internal bool AddGroup;
    internal byte[] Received=>_received.ToArray();
    private static JsonObject Private(string id,string mime)=>new(){["id"]=id,["mimeType"]=mime,["ownedByMe"]=true,["shared"]=false,["trashed"]=false,["owners"]=new JsonArray(new JsonObject{["permissionId"]="fictional-permission-id"}),["permissions"]=new JsonArray(new JsonObject{["id"]="fictional-permission-id",["type"]="user",["role"]="owner"})};
    private static HttpResponseMessage Json(JsonNode body)=>new(HttpStatusCode.OK){Content=new StringContent(body.ToJsonString())};
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();Calls++;Assert.Equal("www.googleapis.com",request.RequestUri!.Host);Assert.Equal("Fictional-TEST-access-token",request.Headers.Authorization?.Parameter);string path=request.RequestUri.AbsolutePath;
        if(path.EndsWith("/generateIds",StringComparison.Ordinal))return Json(new JsonObject{["ids"]=new JsonArray("fictional-generated-"+(++Generated))});
        if(path=="/drive/v3/files/"+policy.FolderId)
        {
            var folder=Private(policy.FolderId,"application/vnd.google-apps.folder");folder["name"]=policy.FolderName;folder["capabilities"]=new JsonObject{["canAddChildren"]=true};folder["appProperties"]=new JsonObject{["sarahBeautyPurpose"]="private-output-v1",["sarahBeautyBinding"]=policy.FolderBinding};return Json(folder);
        }
        if(path.StartsWith("/drive/v3/files/",StringComparison.Ordinal))
        {
            if(_metadata is null || _received.Length==0)return new(HttpStatusCode.NotFound){Content=new StringContent("{}")};var file=Private(_metadata["id"]!.GetValue<string>(),"application/octet-stream");file["parents"]=_metadata["parents"]!.DeepClone();file["appProperties"]=_metadata["appProperties"]!.DeepClone();file["capabilities"]=new JsonObject{["canDownload"]=true};file["size"]=_received.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);file["sha256Checksum"]=Convert.ToHexString(SHA256.HashData(Received));if(AddGroup)file["permissions"]!.AsArray().Add(new JsonObject{["id"]="fictional-group",["type"]="group",["role"]="reader"});return Json(file);
        }
        Assert.Equal("/upload/drive/v3/files",path);
        if(request.Method==HttpMethod.Post)
        {
            Assert.Equal(0,_received.Length);_metadata=JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellation))!.AsObject();var response=new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent([])};response.Headers.Location=new Uri("https://www.googleapis.com/upload/drive/v3/files?uploadType=resumable&upload_id=Fictional-TEST-private-session");return response;
        }
        Assert.Equal(HttpMethod.Put,request.Method);ContentWrites++;Assert.Equal(_received.Length,request.Content!.Headers.ContentRange!.From);byte[] bytes=await request.Content.ReadAsByteArrayAsync(cancellation);_received.Write(bytes);return Json(new JsonObject{["id"]=_metadata!["id"]!.GetValue<string>(),["size"]=_received.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),["sha256Checksum"]=Convert.ToHexString(SHA256.HashData(Received))});
    }
    protected override void Dispose(bool disposing){if(disposing)_received.Dispose();base.Dispose(disposing);}
}
