using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using SarahBeauty.Business.Security;
using global::Google.Apis.Auth.OAuth2.Responses;

namespace SarahBeauty.Business.Google;

internal sealed record GoogleAccessGrant(GoogleConnectionRecord Connection,string AccessToken)
{
    public override string ToString()=>"Google access grant (credentials hidden)";
}
public sealed partial class GoogleConnections
{
    internal Func<GoogleDesktopClient,string,CancellationToken,Task<TokenResponse>> Renew {get;set;}=static(client,token,cancellation)=>new GoogleOAuthProtocol().RefreshAsync(client,token,cancellation);
    internal async Task<GoogleAccessGrant> AccessAsync(CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(TimeSpan.FromMinutes(2));
        try{return await LoadAccess(deadline.Token).ConfigureAwait(false);}
        catch(OperationCanceledException) when(!cancellation.IsCancellationRequested){throw new GoogleConnectionFailure("GoogleRequestTimedOut","Google token renewal did not complete in time. Retry when available.");}
    }
    private async Task<GoogleAccessGrant> LoadAccess(CancellationToken cancellation)
    {
        var loaded=Load()??throw NotConnected();cancellation.ThrowIfCancellationRequested();if(Fresh(loaded.Token))return new(loaded.Connection,loaded.Token.AccessToken);
        using var lease=await RefreshLease(cancellation).ConfigureAwait(false);loaded=Load()??throw NotConnected();cancellation.ThrowIfCancellationRequested();if(Fresh(loaded.Token))return new(loaded.Connection,loaded.Token.AccessToken);
        var renewed=await Renew(loaded.Client.Client,loaded.Token.RefreshToken,cancellation).ConfigureAwait(false);renewed.RefreshToken??=loaded.Token.RefreshToken;
        var material=GoogleGrantPayload.Create(renewed,loaded.Connection.Id,loaded.Connection.ConfigurationId);var account=await VerifyAccount(material.AccessToken,cancellation).ConfigureAwait(false);
        if(account.Account.PermissionId!=loaded.Connection.Account.Account.PermissionId)throw new GoogleConnectionFailure("GoogleAccountChanged","Google token renewal returned another account. Reconnect the intended account.");
        cancellation.ThrowIfCancellationRequested();byte[] encoded=JsonSerializer.SerializeToUtf8Bytes(material);
        try
        {
            _files.Write(DeviceCredentialKind.GoogleToken,encoded,db=>{cancellation.ThrowIfCancellationRequested();GoogleConnectionRecords.RequireConsent(db,loaded.Connection);});return new(loaded.Connection,material.AccessToken);
        }
        finally{CryptographicOperations.ZeroMemory(encoded);}
    }
    internal void RequireAccess(GoogleAccessGrant grant)=>_store.Read(db=>{GoogleConnectionRecords.RequireConsent(db,grant.Connection);return true;});
    private static bool Fresh(GoogleGrantPayload material)=>DateTimeOffset.Parse(material.ExpiresAtUtc,CultureInfo.InvariantCulture)>DateTimeOffset.UtcNow.AddMinutes(2);
    private async Task<FileStream> RefreshLease(CancellationToken cancellation)
    {
        while(true){cancellation.ThrowIfCancellationRequested();var lease=_files.TryRefreshLease();if(lease is not null)return lease;await Task.Delay(100,cancellation).ConfigureAwait(false);}
    }
    private static GoogleConnectionFailure NotConnected()=>new("GoogleConsentUnavailable","This instance has no active Google connection. Reconnect Google.");
}
