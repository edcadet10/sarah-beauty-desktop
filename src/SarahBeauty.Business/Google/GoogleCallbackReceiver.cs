using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using global::Google.Apis.Auth.OAuth2;
using global::Google.Apis.Auth.OAuth2.Requests;
using global::Google.Apis.Auth.OAuth2.Responses;

namespace SarahBeauty.Business.Google;

internal sealed class GoogleCallbackReceiver(ICodeReceiver receiver):ICodeReceiver
{
    private int _used;
    public string RedirectUri=>receiver.RedirectUri;
    public async Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(AuthorizationCodeRequestUrl request,CancellationToken cancellation)
    {
        if(Interlocked.Exchange(ref _used,1)!=0)throw Rejected();
        var redirect=new Uri(request.RedirectUri);if(request.RedirectUri!=RedirectUri || redirect.Scheme!="http" || redirect.Host!="127.0.0.1" || redirect.Port<1 || redirect.AbsolutePath!="/authorize/" || redirect.UserInfo.Length!=0 || redirect.Query.Length!=0 || redirect.Fragment.Length!=0)throw Rejected();
        string state=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));request.State=state;AuthorizationCodeResponseUrl response;
        try{response=await receiver.ReceiveCodeAsync(request,cancellation).ConfigureAwait(false);}
        catch(NotSupportedException){throw new GoogleConnectionFailure("BrowserUnavailable","Windows could not open the Google consent page. Check the default browser and try again.");}
        if(response.State is null || response.State.Length!=state.Length || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state),Encoding.UTF8.GetBytes(response.State)) || response.Code?.Length>4096 || !string.IsNullOrEmpty(response.Code) && !string.IsNullOrEmpty(response.Error))throw Rejected();
        return response;
    }
    private static GoogleConnectionFailure Rejected()=>new("CallbackRejected","The Google callback did not match this connection attempt. Start a new connection.");
}

internal class GoogleSystemBrowserReceiver():LocalServerCodeReceiver("<!doctype html><html><head><title>Sarah Beauty</title></head><body>Return to Sarah Beauty to review the connection result. You can close this tab.</body></html>",CallbackUriChooserStrategy.ForceLoopbackIp)
{
    protected override bool OpenBrowser(string url)
    {
        var address=new Uri(url);if(address.Scheme!="https" || address.Host!="accounts.google.com" || !address.IsDefaultPort || address.UserInfo.Length!=0)throw new NotSupportedException();
        return Process.Start(new ProcessStartInfo(address.AbsoluteUri){UseShellExecute=true}) is not null;
    }
}
