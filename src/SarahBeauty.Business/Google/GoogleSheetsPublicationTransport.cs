using System.Net.Http.Json;

namespace SarahBeauty.Business.Google;

internal sealed class GoogleSheetsPublicationTransport(HttpClient? client=null)
{
    internal async Task<GoogleSheetsFileObservation> PublishAsync(GoogleSheetsFileSpec spec,string fileId,string account,Func<CancellationToken,Task<string>> access,CancellationToken cancellation=default)
    {
        var files=new GoogleSheetsFiles(client);var before=await files.VerifyAsync(spec,fileId,account,access,cancellation,requiredState:null).ConfigureAwait(false);
        if(before.State=="staging")
        {
            cancellation.ThrowIfCancellationRequested();string token=await access(cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();
            using var request=GoogleDriveFolders.Request(HttpMethod.Patch,"https://www.googleapis.com/drive/v3/files/"+fileId+"?prettyPrint=false&fields=id",token);
            request.Content=JsonContent.Create(new{appProperties=new Dictionary<string,string>{{"sarahBeautyExportState","verified"}}});
            await GoogleApiJson.ReadAsync(client??GoogleApiJson.Client,request,root=>
            {
                if(root.GetProperty("id").GetString()!=fileId)throw new GoogleConnectionFailure("ReadablePublicationUnconfirmed","Google did not confirm the expected publication file. Independent verification is still required.");return true;
            },cancellation).ConfigureAwait(false);
        }
        var verified=await files.VerifyAsync(spec,fileId,account,access,cancellation,requiredState:"verified").ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();return verified;
    }
}
