using System.Globalization;
using System.Text.Json;

namespace SarahBeauty.Business.Google;

internal sealed record GoogleArchiveUploadSpec(string FileId,string FolderId,string FolderBinding,string JobId,long ByteLength,string Sha256)
{
    internal void Validate()
    {
        GoogleDriveFolders.RequireId(FileId);GoogleDriveFolders.RequireId(FolderId);
        if(!Guid.TryParseExact(FolderBinding,"N",out var binding)||binding==Guid.Empty||!Guid.TryParseExact(JobId,"N",out var job)||job==Guid.Empty||ByteLength is <1 or >64L*1024*1024*1024||Sha256 is not{Length:64}||!Sha256.All(Uri.IsHexDigit))throw new ArgumentException("Complete verified archive identity, size, hash, and destination facts are required.");
    }
    internal object Metadata()=>new{id=FileId,name="SarahBeauty-archive-"+JobId+".age",mimeType="application/octet-stream",parents=new[]{FolderId},appProperties=new Dictionary<string,string>{{"sarahBeautyPurpose","encrypted-archive-v1"},{"sarahBeautyBinding",FolderBinding},{"sarahBeautyArchiveJob",JobId},{"sarahBeautyArchiveSha256",Sha256}}};
    internal GoogleUploadedArchive Completion(byte[] bytes)
    {
        using var json=JsonDocument.Parse(bytes,new(){MaxDepth=16});return Completion(json.RootElement);
    }
    internal GoogleUploadedArchive Completion(JsonElement root)
    {
        if(root.GetProperty("id").GetString()!=FileId || root.GetProperty("size").ValueKind!=JsonValueKind.String || !long.TryParse(root.GetProperty("size").GetString(),NumberStyles.None,CultureInfo.InvariantCulture,out long size) || size!=ByteLength)throw Unconfirmed();
        string? hash=root.TryGetProperty("sha256Checksum",out var checksum)?checksum.GetString():null;if(hash is not{Length:64}||!hash.All(Uri.IsHexDigit)||!string.Equals(hash,Sha256,StringComparison.OrdinalIgnoreCase))throw Unconfirmed();
        return new(FileId,size,hash.ToUpperInvariant());
    }
    internal static GoogleConnectionFailure Unconfirmed()=>new("GoogleArchiveUnconfirmed","Google archive identity, size, checksum, or upload progress could not be confirmed. Preserve the last verified backup and review this attempt.");
}
internal sealed record GoogleUploadedArchive(string FileId,long ByteLength,string Sha256);
internal sealed record GoogleUploadProgress(long Offset,GoogleUploadedArchive? Completed=null);
internal sealed class GoogleUploadSession(Uri address)
{
    internal Uri Address {get;}=address;
    internal static GoogleUploadSession Read(HttpResponseMessage response)
    {
        var uri=response.Headers.Location;
        if(response.StatusCode is not(System.Net.HttpStatusCode.OK or System.Net.HttpStatusCode.Created) || uri is null || !uri.IsAbsoluteUri || uri.Scheme!="https" || uri.Host!="www.googleapis.com" || !uri.IsDefaultPort || uri.AbsolutePath!="/upload/drive/v3/files" || uri.UserInfo.Length!=0 || uri.Fragment.Length!=0 || uri.Query.Length is <1 or >8192 || !uri.Query.Contains("upload_id=",StringComparison.Ordinal))throw new GoogleConnectionFailure("GoogleUploadLocationRejected","Google returned an unsupported upload location. No archive content was sent there.");return new(uri);
    }
    public override string ToString()=>"Google resumable upload session (private location hidden)";
}
