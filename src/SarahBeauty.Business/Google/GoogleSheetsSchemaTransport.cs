using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace SarahBeauty.Business.Google;

internal sealed class GoogleSheetsSchemaTransport(HttpClient? client=null)
{
    private HttpClient Client=>client??GoogleApiJson.Client;
    internal async Task EnsureAsync(GoogleSheetsWorkbookSchema schema,GoogleSheetsFileSpec spec,string fileId,string account,Func<CancellationToken,Task<string>> access,CancellationToken cancellation=default)
    {
        spec.Validate();GoogleDriveFolders.RequireId(fileId);GoogleDriveFolders.RequireId(account);
        if(spec.GenerationId!=schema.GenerationId||spec.ManifestSha256!=schema.ManifestSha256)throw new GoogleConnectionFailure("ReadableSchemaSourceMismatch","The workbook layout and job do not identify the same verified readable generation.");
        var files=new GoogleSheetsFiles(Client);
        async Task Private()=>await files.VerifyAsync(spec,fileId,account,access,cancellation).ConfigureAwait(false);
        await Private().ConfigureAwait(false);
        using var inspect=await Request(HttpMethod.Get,Address(fileId),access,cancellation).ConfigureAwait(false);
        var changes=await GoogleApiJson.ReadAsync(Client,inspect,root=>GoogleSheetsSchemaInitialization.Plan(schema,root,fileId),cancellation).ConfigureAwait(false);
        if(changes.Operations>0)
        {
            await Private().ConfigureAwait(false);await Apply(fileId,changes,access,cancellation).ConfigureAwait(false);
            using var verify=await Request(HttpMethod.Get,Address(fileId),access,cancellation).ConfigureAwait(false);
            await GoogleApiJson.ReadAsync(Client,verify,root=>{schema.Require(root,fileId);return true;},cancellation).ConfigureAwait(false);
        }
        await Private().ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();
    }

    private async Task Apply(string fileId,GoogleSheetsSchemaChanges changes,Func<CancellationToken,Task<string>> access,CancellationToken cancellation)
    {
        byte[] bytes=changes.Json.ToArray();
        try
        {
            using var request=await Request(HttpMethod.Post,"https://sheets.googleapis.com/v4/spreadsheets/"+fileId+":batchUpdate?prettyPrint=false&fields=spreadsheetId",access,cancellation).ConfigureAwait(false);
            request.Content=new ByteArrayContent(bytes);request.Content.Headers.ContentType=new MediaTypeHeaderValue("application/json");
            await GoogleApiJson.ReadAsync(Client,request,root=>
            {
                if(root.GetProperty("spreadsheetId").GetString()!=fileId)throw new GoogleConnectionFailure("ReadableSchemaUnconfirmed","Google did not confirm the expected workbook layout update. Preserve this job and inspect its schema before retrying.");return true;
            },cancellation).ConfigureAwait(false);
        }
        finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    private static string Address(string fileId)=>"https://sheets.googleapis.com/v4/spreadsheets/"+fileId+"?prettyPrint=false&fields="+Uri.EscapeDataString(GoogleSheetsWorkbookSchema.Fields);
    private static async Task<HttpRequestMessage> Request(HttpMethod method,string address,Func<CancellationToken,Task<string>> access,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();string token=await access(cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();return GoogleDriveFolders.Request(method,address,token);
    }
}
