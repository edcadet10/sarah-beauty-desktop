using global::Google.Apis.Auth.OAuth2;
using global::Google.Apis.Auth.OAuth2.Flows;
using global::Google.Apis.Auth.OAuth2.Responses;
using global::Google.Apis.Http;

namespace SarahBeauty.Business.Google;

internal sealed class GoogleOAuthProtocol(Func<ICodeReceiver>? receivers=null,IHttpClientFactory? clients=null,TimeSpan? timeout=null)
{
    internal const string DriveFileScope="https://www.googleapis.com/auth/drive.file";
    internal async Task<TokenResponse> AuthorizeAsync(GoogleDesktopClient client,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(timeout??TimeSpan.FromMinutes(5));
        using var flow=new PkceGoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets=new(){ClientId=client.ClientId,ClientSecret=client.ClientSecret},ProjectId=client.ProjectId,Scopes=[DriveFileScope],
            Prompt="consent select_account",IncludeGrantedScopes=false,DataStore=null,HttpClientFactory=clients
        });
        var receiver=new GoogleCallbackReceiver(receivers?.Invoke()??new GoogleSystemBrowserReceiver());
        try
        {
            var credential=await new AuthorizationCodeInstalledApp(flow,receiver).AuthorizeAsync("SarahBeauty-desktop-connection",deadline.Token).ConfigureAwait(false);
            Validate(credential.Token);return credential.Token;
        }
        catch(OperationCanceledException) when(!cancellation.IsCancellationRequested){throw new GoogleConnectionFailure("ConsentTimedOut","Google consent timed out. Start a new connection when ready.");}
        catch(TokenResponseException error){throw FromTokenError(error.Error?.Error);}
        catch(HttpRequestException){throw new GoogleConnectionFailure("GoogleUnavailable","Google could not be reached. Check the connection and retry.");}
        catch(Exception error) when(error is System.Net.HttpListenerException or System.Net.Sockets.SocketException){throw new GoogleConnectionFailure("CallbackUnavailable","The local Google callback could not open. Check local network permissions and retry.");}
        catch(GoogleConnectionFailure){throw;}
        catch(Exception error) when(error is IOException or InvalidOperationException or FormatException or Newtonsoft.Json.JsonException){throw new GoogleConnectionFailure("InvalidGoogleResponse","Google authorization returned an incomplete response. Start a new connection.");}
    }
    private static void Validate(TokenResponse token)
    {
        if(string.IsNullOrWhiteSpace(token.AccessToken)||token.AccessToken.Length>16384||token.AccessToken.Any(char.IsWhiteSpace)||string.IsNullOrWhiteSpace(token.RefreshToken)||token.RefreshToken.Length>16384||token.RefreshToken.Any(char.IsControl)||!string.Equals(token.TokenType,"Bearer",StringComparison.OrdinalIgnoreCase)||token.ExpiresInSeconds is null or <1 or >604800)throw new GoogleConnectionFailure("IncompleteGrant","Google did not return a complete offline connection. Start a new connection and review consent.");
        if(token.Scope is not null && !token.Scope.Split(' ',StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal).SetEquals([DriveFileScope]))throw new GoogleConnectionFailure("UnexpectedScope","Google returned permissions outside this app's file access. Review the app grant and start a new connection.");
        token.Scope=DriveFileScope;
    }
    private static GoogleConnectionFailure FromTokenError(string? code)=>code switch
    {
        "access_denied"=>new("ConsentDenied","Google access was declined. No desktop connection was saved."),
        "invalid_client" or "unauthorized_client"=>new("ClientUnavailable","Google did not accept the configured Desktop client. Review its project configuration."),
        "invalid_grant"=>new("GrantUnavailable","Google did not accept this authorization. Start a new connection."),
        _=>new("AuthorizationFailed","Google authorization did not complete. Review consent and try again.")
    };
}
