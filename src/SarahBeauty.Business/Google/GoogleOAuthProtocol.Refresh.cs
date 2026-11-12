using global::Google.Apis.Auth.OAuth2.Responses;

namespace SarahBeauty.Business.Google;

internal sealed partial class GoogleOAuthProtocol
{
    internal async Task<TokenResponse> RefreshAsync(GoogleDesktopClient client,string refreshToken,CancellationToken cancellation=default)
    {
        if(string.IsNullOrWhiteSpace(refreshToken)||refreshToken.Length>16384||refreshToken.Any(char.IsControl))throw new GoogleConnectionFailure("IncompleteGrant","The saved Google refresh material is incomplete. Reconnect Google.");
        cancellation.ThrowIfCancellationRequested();using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(Deadline(false));using var flow=CreateFlow(client,false);
        try
        {
            var token=await flow.RefreshTokenAsync("SarahBeauty-desktop-connection",refreshToken,deadline.Token).ConfigureAwait(false);token.RefreshToken??=refreshToken;Validate(token);return token;
        }
        catch(OperationCanceledException) when(!cancellation.IsCancellationRequested){throw new GoogleConnectionFailure("GoogleRequestTimedOut","Google token renewal timed out. Check the connection and retry.");}
        catch(TokenResponseException error){throw FromTokenError(error.Error?.Error);}
        catch(HttpRequestException){throw new GoogleConnectionFailure("GoogleUnavailable","Google could not be reached. Check the connection and retry.");}
        catch(GoogleConnectionFailure){throw;}
        catch(Exception error) when(error is IOException or InvalidOperationException or FormatException or Newtonsoft.Json.JsonException){throw new GoogleConnectionFailure("InvalidGoogleResponse","Google returned an incomplete token renewal response. Review the connection.");}
    }
}
