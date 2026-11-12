using System.Net.Http.Json;
using System.Text.Json;

namespace SarahBeauty.Business.Google;

internal sealed class GoogleSheetsFiles(HttpClient? client=null)
{
    private const string Root="https://www.googleapis.com/drive/v3/files";
    private HttpClient Client=>client??GoogleApiJson.Client;

    internal async Task<string> CreateEmptyAsync(GoogleSheetsFileSpec spec,Func<CancellationToken,Task<string>> access,CancellationToken cancellation=default)
    {
        spec.Validate();using var request=await Request(HttpMethod.Post,Root+"?ignoreDefaultVisibility=true&prettyPrint=false&fields=id",access,cancellation).ConfigureAwait(false);
        request.Content=JsonContent.Create(spec.Metadata());
        return await GoogleApiJson.ReadAsync(Client,request,root=>{string id=root.GetProperty("id").GetString()??"";GoogleDriveFolders.RequireId(id);return id;},cancellation).ConfigureAwait(false);
    }

    internal async Task<GoogleSheetsFileObservation> VerifyAsync(GoogleSheetsFileSpec spec,string fileId,string account,Func<CancellationToken,Task<string>> access,CancellationToken cancellation=default,string? requiredState="staging")
    {
        spec.Validate();GoogleDriveFolders.RequireId(fileId);GoogleDriveFolders.RequireId(account);
        using var request=await Request(HttpMethod.Get,Root+"/"+fileId+"?prettyPrint=false&fields="+Uri.EscapeDataString(GoogleSheetsFileSpec.Fields),access,cancellation).ConfigureAwait(false);
        return await GoogleApiJson.ReadAsync(Client,request,root=>spec.Verify(root,fileId,account,requiredState),cancellation).ConfigureAwait(false);
    }

    internal async Task<GoogleSheetsFileObservation?> FindAsync(GoogleSheetsFileSpec spec,string account,Func<CancellationToken,Task<string>> access,CancellationToken cancellation=default)
    {
        spec.Validate();GoogleDriveFolders.RequireId(account);
        string query=$"'{spec.FolderId}' in parents and trashed=false and appProperties has {{ key='sarahBeautyExportJob' and value='{spec.JobId}' }}";
        string fields="nextPageToken,incompleteSearch,files("+GoogleSheetsFileSpec.Fields+")";
        using var request=await Request(HttpMethod.Get,Root+"?spaces=drive&pageSize=2&prettyPrint=false&q="+Uri.EscapeDataString(query)+"&fields="+Uri.EscapeDataString(fields),access,cancellation).ConfigureAwait(false);
        return await GoogleApiJson.ReadAsync(Client,request,root=>Find(root,spec,account),cancellation).ConfigureAwait(false);
    }

    private static GoogleSheetsFileObservation? Find(JsonElement root,GoogleSheetsFileSpec spec,string account)
    {
        var files=root.GetProperty("files");
        if(files.GetArrayLength()>1||root.TryGetProperty("nextPageToken",out var page)&&!string.IsNullOrEmpty(page.GetString())||
            root.TryGetProperty("incompleteSearch",out var incomplete)&&incomplete.ValueKind!=JsonValueKind.False)
            throw new GoogleConnectionFailure("ReadableWorkbookLookupUnconfirmed","The job workbook lookup is incomplete or ambiguous. Review the existing attempt before creating another file.");
        if(files.GetArrayLength()==0)return null;string id=files[0].GetProperty("id").GetString()??"";return spec.Verify(files[0],id,account,requiredState:null);
    }

    private static async Task<HttpRequestMessage> Request(HttpMethod method,string address,Func<CancellationToken,Task<string>> access,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();string token=await access(cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();return GoogleDriveFolders.Request(method,address,token);
    }
}
