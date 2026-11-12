using System.Globalization;
using global::Google.Apis.Auth.OAuth2.Responses;

namespace SarahBeauty.Business.Google;

internal sealed class GoogleGrantPayload
{
    public int Version {get;set;}=1;
    public string ConnectionId {get;set;}="";
    public string ConfigurationId {get;set;}="";
    public string AccessToken {get;set;}="";
    public string RefreshToken {get;set;}="";
    public string TokenType {get;set;}="";
    public string Scope {get;set;}="";
    public long LifetimeSeconds {get;set;}
    public string ExpiresAtUtc {get;set;}="";
    public override string ToString()=>"Protected Google grant (credentials hidden)";
    internal static GoogleGrantPayload Create(TokenResponse token,string connection,string configuration)
    {
        GoogleOAuthProtocol.Validate(token);return new(){ConnectionId=connection,ConfigurationId=configuration,AccessToken=token.AccessToken,RefreshToken=token.RefreshToken,TokenType=token.TokenType,Scope=token.Scope,LifetimeSeconds=token.ExpiresInSeconds!.Value,ExpiresAtUtc=DateTimeOffset.UtcNow.AddSeconds(token.ExpiresInSeconds.Value).ToString("O")};
    }
    internal void Validate(GoogleConnectionRecord record)
    {
        if(Version!=1 || ConnectionId!=record.Id || ConfigurationId!=record.ConfigurationId || Scope!=GoogleOAuthProtocol.DriveFileScope || !DateTimeOffset.TryParse(ExpiresAtUtc,CultureInfo.InvariantCulture,DateTimeStyles.None,out _))throw new GoogleConnectionFailure("GrantBindingMismatch","The protected Google grant does not match this connection. Reconnect Google.");
        GoogleOAuthProtocol.Validate(new TokenResponse{AccessToken=AccessToken,RefreshToken=RefreshToken,TokenType=TokenType,Scope=Scope,ExpiresInSeconds=LifetimeSeconds});
    }
}
internal sealed record LoadedGoogleGrant(ConfiguredGoogleClient Client,GoogleConnectionRecord Connection,GoogleGrantPayload Token)
{
    public override string ToString()=>"Loaded Google connection (credentials hidden)";
}
