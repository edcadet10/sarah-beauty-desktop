using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace SarahBeauty.Business.Google;

internal sealed partial class GoogleArchiveUploads(HttpClient? client=null)
{
    internal const int ChunkBytes=4*1024*1024;
    private HttpClient Client=>client??GoogleApiJson.Client;
    internal async Task<string> GenerateIdAsync(string token,CancellationToken cancellation=default)
    {
        using var request=GoogleDriveFolders.Request(HttpMethod.Get,"https://www.googleapis.com/drive/v3/files/generateIds?count=1&space=drive&type=files",token);
        return await GoogleApiJson.ReadAsync(Client,request,root=>{var ids=root.GetProperty("ids");if(ids.GetArrayLength()!=1)throw GoogleArchiveUploadSpec.Unconfirmed();string id=ids[0].GetString()??"";GoogleDriveFolders.RequireId(id);return id;},cancellation).ConfigureAwait(false);
    }
    internal async Task<GoogleUploadedArchive> UploadAsync(Stream encrypted,GoogleArchiveUploadSpec spec,Func<CancellationToken,Task<string>> access,CancellationToken cancellation=default)
    {
        spec.Validate();if(!encrypted.CanRead || !encrypted.CanSeek || encrypted.Length!=spec.ByteLength)throw new InvalidDataException("The encrypted archive source is unavailable or changed.");using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(TimeSpan.FromHours(2));
        try
        {
            encrypted.Position=0;byte[] header=new byte["age-encryption.org/v1\n"u8.Length];await encrypted.ReadExactlyAsync(header,deadline.Token).ConfigureAwait(false);if(!header.AsSpan().SequenceEqual("age-encryption.org/v1\n"u8))throw new InvalidDataException("Choose a completed age-encrypted archive.");encrypted.Position=0;string hash=Convert.ToHexString(await SHA256.HashDataAsync(encrypted,deadline.Token).ConfigureAwait(false));if(!string.Equals(hash,spec.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("The encrypted archive source checksum changed.");
            var session=await Begin(spec,access,deadline.Token).ConfigureAwait(false);return await Transfer(encrypted,spec,session,access,deadline.Token).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(!cancellation.IsCancellationRequested){throw new GoogleConnectionFailure("GoogleRequestTimedOut","The archive upload did not finish within its deadline. Preserve this attempt for review and retry.");}
    }
    private async Task<GoogleUploadSession> Begin(GoogleArchiveUploadSpec spec,Func<CancellationToken,Task<string>> access,CancellationToken cancellation)
    {
        using var request=GoogleDriveFolders.Request(HttpMethod.Post,"https://www.googleapis.com/upload/drive/v3/files?uploadType=resumable&ignoreDefaultVisibility=true&fields=id,size,sha256Checksum",await access(cancellation).ConfigureAwait(false));request.Content=JsonContent.Create(spec.Metadata());
        request.Headers.Add("X-Upload-Content-Type","application/octet-stream");request.Headers.Add("X-Upload-Content-Length",spec.ByteLength.ToString(CultureInfo.InvariantCulture));
        return await GoogleApiJson.SendAsync(Client,request,(response,_)=>GoogleUploadSession.Read(response),cancellation).ConfigureAwait(false);
    }
    private async Task<GoogleUploadedArchive> Transfer(Stream source,GoogleArchiveUploadSpec spec,GoogleUploadSession session,Func<CancellationToken,Task<string>> access,CancellationToken cancellation)
    {
        byte[] buffer=new byte[ChunkBytes];long offset=0;int recoveries=0;
        try
        {
            while(offset<spec.ByteLength)
            {
                cancellation.ThrowIfCancellationRequested();source.Position=offset;int count=(int)Math.Min(buffer.Length,spec.ByteLength-offset);await source.ReadExactlyAsync(buffer.AsMemory(0,count),cancellation).ConfigureAwait(false);GoogleUploadProgress progress;
                try{progress=await Put(session,spec,buffer,count,offset,access,cancellation).ConfigureAwait(false);if(progress.Completed is null && progress.Offset<=offset)throw GoogleArchiveUploadSpec.Unconfirmed();}
                catch(GoogleConnectionFailure error) when((error.Code is "GoogleUnavailable" or "GoogleRequestTimedOut") && recoveries<3)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1<<recoveries++),cancellation).ConfigureAwait(false);progress=await Put(session,spec,[],0,offset+count,access,cancellation).ConfigureAwait(false);if(progress.Offset<offset)throw GoogleArchiveUploadSpec.Unconfirmed();
                }
                if(progress.Completed is not null)return progress.Completed;offset=progress.Offset;
            }
            throw GoogleArchiveUploadSpec.Unconfirmed();
        }
        finally{CryptographicOperations.ZeroMemory(buffer);}
    }
    private async Task<GoogleUploadProgress> Put(GoogleUploadSession session,GoogleArchiveUploadSpec spec,byte[] buffer,int count,long offset,Func<CancellationToken,Task<string>> access,CancellationToken cancellation)
    {
        using var request=GoogleDriveFolders.Request(HttpMethod.Put,session.Address.AbsoluteUri,await access(cancellation).ConfigureAwait(false));request.Content=new ByteArrayContent(buffer,0,count);request.Content.Headers.ContentType=new MediaTypeHeaderValue("application/octet-stream");request.Content.Headers.ContentRange=count==0?new ContentRangeHeaderValue(spec.ByteLength):new ContentRangeHeaderValue(offset,offset+count-1,spec.ByteLength);
        return await GoogleApiJson.SendAsync(Client,request,(response,bytes)=>Progress(response,bytes,spec,count==0?offset:offset+count),cancellation,acceptResumeIncomplete:true).ConfigureAwait(false);
    }
    private static GoogleUploadProgress Progress(HttpResponseMessage response,byte[] bytes,GoogleArchiveUploadSpec spec,long maximum)
    {
        if((int)response.StatusCode!=308){if(maximum!=spec.ByteLength)throw GoogleArchiveUploadSpec.Unconfirmed();return new(spec.ByteLength,spec.Completion(bytes));}
        if(!response.Headers.TryGetValues("Range",out var headers))return new(0);string[] values=headers.ToArray();if(values.Length!=1 || !values[0].StartsWith("bytes=0-",StringComparison.Ordinal) || !long.TryParse(values[0].AsSpan(8),NumberStyles.None,CultureInfo.InvariantCulture,out long end) || end<0 || end>=spec.ByteLength || end>=maximum || end==long.MaxValue)throw GoogleArchiveUploadSpec.Unconfirmed();return new(end+1);
    }
}
