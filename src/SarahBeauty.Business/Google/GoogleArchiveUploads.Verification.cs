using System.Text.Json;

namespace SarahBeauty.Business.Google;

internal sealed partial class GoogleArchiveUploads
{
    internal async Task<GoogleUploadedArchive?> FindVerifiedAsync(GoogleArchiveUploadSpec spec,string accountPermissionId,Func<CancellationToken,Task<string>> access,CancellationToken cancellation=default)
    {
        spec.Validate();GoogleDriveFolders.RequireId(accountPermissionId);string fields=GoogleDrivePrivacy.Fields+",parents,appProperties,size,sha256Checksum,capabilities(canDownload)";
        using var request=GoogleDriveFolders.Request(HttpMethod.Get,"https://www.googleapis.com/drive/v3/files/"+spec.FileId+"?fields="+Uri.EscapeDataString(fields),await access(cancellation).ConfigureAwait(false));
        try{return await GoogleApiJson.ReadAsync(Client,request,root=>Verify(root,spec,accountPermissionId),cancellation).ConfigureAwait(false);}
        catch(GoogleConnectionFailure error) when(error.Code=="GoogleFileUnavailable"){return null;}
    }
    private static GoogleUploadedArchive Verify(JsonElement root,GoogleArchiveUploadSpec spec,string account)
    {
        if(!GoogleDrivePrivacy.OnlyOwner(root,spec.FileId,"application/octet-stream",account) || root.GetProperty("capabilities").GetProperty("canDownload").ValueKind!=JsonValueKind.True)throw new GoogleConnectionFailure("GoogleArchiveNotPrivate","The Google archive is not a downloadable private file owned only by the connected account. Review its access before treating it as a backup.");
        var parents=root.GetProperty("parents");var properties=root.GetProperty("appProperties");
        if(parents.GetArrayLength()!=1 || parents[0].GetString()!=spec.FolderId || properties.GetProperty("sarahBeautyPurpose").GetString()!="encrypted-archive-v1" || properties.GetProperty("sarahBeautyBinding").GetString()!=spec.FolderBinding || properties.GetProperty("sarahBeautyArchiveJob").GetString()!=spec.JobId || properties.GetProperty("sarahBeautyArchiveSha256").GetString()!=spec.Sha256)throw GoogleArchiveUploadSpec.Unconfirmed();return spec.Completion(root);
    }
}
