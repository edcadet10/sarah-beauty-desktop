using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace SarahBeauty.Business.Google;

internal static class GoogleApiJson
{
    internal const int MaximumResponseBytes=2*1024*1024;
    internal static readonly HttpClient Client=new(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false,AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate}){Timeout=TimeSpan.FromSeconds(60)};
    internal static async Task<T> ReadAsync<T>(HttpClient client,HttpRequestMessage request,Func<JsonElement,T> project,CancellationToken cancellation)
        =>await SendAsync(client,request,(_,bytes)=>{using var json=JsonDocument.Parse(bytes,new(){MaxDepth=32});return project(json.RootElement);},cancellation).ConfigureAwait(false);
    internal static async Task<T> SendAsync<T>(HttpClient client,HttpRequestMessage request,Func<HttpResponseMessage,byte[],T> project,CancellationToken cancellation,bool acceptResumeIncomplete=false)
    {
        byte[]? bytes=null;using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(client.Timeout==Timeout.InfiniteTimeSpan || client.Timeout>TimeSpan.FromSeconds(60)?TimeSpan.FromSeconds(60):client.Timeout);
        try
        {
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token).ConfigureAwait(false);bytes=await Body(response.Content,deadline.Token).ConfigureAwait(false);
            if(!response.IsSuccessStatusCode && !(acceptResumeIncomplete && (int)response.StatusCode==308))throw Failure(response.StatusCode,bytes);return project(response,bytes);
        }
        catch(OperationCanceledException) when(!cancellation.IsCancellationRequested){throw new GoogleConnectionFailure("GoogleRequestTimedOut","The Google request timed out. Check the connection and retry.");}
        catch(HttpRequestException){throw new GoogleConnectionFailure("GoogleUnavailable","Google could not be reached. Check the connection and retry.");}
        catch(GoogleConnectionFailure){throw;}
        catch(IOException){throw new GoogleConnectionFailure("GoogleUnavailable","The Google response could not be read. Check the connection and retry.");}
        catch(Exception error) when(error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException){throw new GoogleConnectionFailure("InvalidGoogleResponse","Google returned an incomplete or unsupported response. Retry after reviewing the connection.");}
        finally{if(bytes is not null)CryptographicOperations.ZeroMemory(bytes);}
    }
    private static async Task<byte[]> Body(HttpContent content,CancellationToken cancellation)
    {
        if(content.Headers.ContentLength>MaximumResponseBytes)throw TooLarge();using var stream=await content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);using var output=new MemoryStream();byte[] buffer=new byte[16384];int count;
        try
        {
            while((count=await stream.ReadAsync(buffer,cancellation).ConfigureAwait(false))>0){if(output.Length+count>MaximumResponseBytes)throw TooLarge();output.Write(buffer,0,count);}return output.ToArray();
        }
        finally{CryptographicOperations.ZeroMemory(buffer);if(output.TryGetBuffer(out var written))CryptographicOperations.ZeroMemory(written.AsSpan());}
    }
    private static GoogleConnectionFailure TooLarge()=>new("GoogleResponseTooLarge","Google returned more data than this operation supports. Review the request before retrying.");
    private static GoogleConnectionFailure Failure(HttpStatusCode status,byte[] body)
    {
        string? reason=Reason(body);
        if(status==HttpStatusCode.Unauthorized)return new("GrantUnavailable","Google no longer accepts this connection. Reconnect Google.");
        if(status==HttpStatusCode.NotFound)return new("GoogleFileUnavailable","The requested Google file or upload session is unavailable to this connection. Review the destination and attempt.");
        if(status==HttpStatusCode.Conflict)return new("GoogleFileAlreadyExists","Google already has this file identity. Verify the earlier attempt before retrying; no existing file was overwritten.");
        if((int)status==429 || reason is "rateLimitExceeded" or "userRateLimitExceeded" or "dailyLimitExceeded" or "quotaExceeded" or "RATE_LIMIT_EXCEEDED")return new("GoogleQuotaLimited","Google request quota is unavailable. Wait before retrying; no paid fallback is enabled.");
        if(reason=="storageQuotaExceeded")return new("GoogleStorageFull","Google storage is full. Review existing storage or choose another confirmed destination; no purchase is made.");
        if(status==HttpStatusCode.Forbidden)return new("DriveAccessUnavailable","Google Drive access is unavailable. Review API enablement and account permissions.");
        return new((int)status>=500?"GoogleUnavailable":"GoogleRequestRejected","Google could not complete this request. Review the connection and retry when available.");
    }
    private static string? Reason(byte[] body)
    {
        try
        {
            using var json=JsonDocument.Parse(body,new(){MaxDepth=16});if(!json.RootElement.TryGetProperty("error",out var error))return null;
            foreach(string collection in new[]{"errors","details"})if(error.TryGetProperty(collection,out var items) && items.ValueKind==JsonValueKind.Array)foreach(var item in items.EnumerateArray())if(item.TryGetProperty("reason",out var reason)&&reason.ValueKind==JsonValueKind.String)return reason.GetString();return null;
        }
        catch(Exception error) when(error is JsonException or InvalidOperationException){return null;}
    }
}
