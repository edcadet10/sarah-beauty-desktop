using global::Google.Apis.Http;

namespace SarahBeauty.Business.Google;

internal sealed class GoogleOAuthTransport:HttpClientFactory
{
    protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs args)=>new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false};
}
