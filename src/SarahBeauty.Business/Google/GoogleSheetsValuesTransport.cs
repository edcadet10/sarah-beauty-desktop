using System.Net.Http.Headers;
using System.Security.Cryptography;
using SarahBeauty.Business.Exports;

namespace SarahBeauty.Business.Google;

internal sealed class GoogleSheetsValuesTransport(HttpClient? client=null)
{
    private HttpClient Client=>client??GoogleApiJson.Client;

    internal async Task WriteAsync(string spreadsheetId,GoogleSheetsValueBatch batch,Func<CancellationToken,Task<string>> access,CancellationToken cancellation=default)
    {
        GoogleDriveFolders.RequireId(spreadsheetId);byte[] payload=GoogleSheetsValueRequests.CopyPayload(batch,cancellation);
        try
        {
            cancellation.ThrowIfCancellationRequested();string token=await access(cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();
            using var request=GoogleDriveFolders.Request(HttpMethod.Post,"https://sheets.googleapis.com/v4/spreadsheets/"+spreadsheetId+"/values:batchUpdate?prettyPrint=false&fields=spreadsheetId",token);
            request.Content=new ByteArrayContent(payload);request.Content.Headers.ContentType=new MediaTypeHeaderValue("application/json");
            await GoogleApiJson.ReadAsync(Client,request,root=>
            {
                if(root.GetProperty("spreadsheetId").GetString()!=spreadsheetId)throw new GoogleConnectionFailure("ReadableWriteUnconfirmed","Google did not confirm the expected workbook. Independent readback is still required.");return true;
            },cancellation).ConfigureAwait(false);
        }
        finally{CryptographicOperations.ZeroMemory(payload);}
    }

    internal async Task ReadAsync(GoogleSheetsGridReadRequest expected,Func<CancellationToken,Task<string>> access,CancellationToken cancellation=default)
    {
        var rectangle=GoogleSheetsRequestShapes.Copy(expected.Expected,new ReadableExportBudget(cancellation:cancellation));
        var address=GoogleSheetsGridReadPlan.Address(expected.SpreadsheetId,rectangle);
        if(!string.Equals(expected.Uri.AbsoluteUri,address.AbsoluteUri,StringComparison.Ordinal)||expected.SheetId<0||expected.EstimatedResponseBytes!=GoogleSheetsReadSlices.Estimate(rectangle)||expected.EstimatedResponseBytes>GoogleSheetsReadSlices.MaximumEstimatedBytes)
            throw new GoogleConnectionFailure("ReadableReadLocationRejected","The readable grid request does not match its pinned workbook, range, and response budget.");
        cancellation.ThrowIfCancellationRequested();string token=await access(cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();
        using var request=GoogleDriveFolders.Request(HttpMethod.Get,address.AbsoluteUri,token);request.Headers.CacheControl=new CacheControlHeaderValue{NoCache=true,NoStore=true};
        await GoogleApiJson.ReadAsync(Client,request,root=>{GoogleSheetsGridReadback.Require(root,expected.SpreadsheetId,expected.SheetId,rectangle,cancellation);return true;},cancellation).ConfigureAwait(false);
    }
}
