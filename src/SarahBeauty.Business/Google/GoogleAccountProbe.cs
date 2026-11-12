using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SarahBeauty.Business.Google;

public sealed record GoogleAccountIdentity(string PermissionId,string EmailAddress,string DisplayName)
{
    public override string ToString()=>"Google account (identity hidden)";
}
public sealed record GoogleAccountDetails(GoogleAccountIdentity Account,long? StorageLimitBytes,long? StorageUsageBytes,string ObservedAtUtc)
{
    public long? ObservedFreeBytes=>StorageLimitBytes is long limit && StorageUsageBytes is long usage?Math.Max(0,limit-usage):null;
}
internal sealed class GoogleAccountProbe(HttpClient? client=null)
{
    private const string Address="https://www.googleapis.com/drive/v3/about?fields=user(permissionId,emailAddress,displayName),storageQuota(limit,usage)";
    internal async Task<GoogleAccountDetails> ReadAsync(string accessToken,CancellationToken cancellation=default)
    {
        if(string.IsNullOrWhiteSpace(accessToken)||accessToken.Length>16384||accessToken.Any(c=>char.IsWhiteSpace(c)||char.IsControl(c)))throw new GoogleConnectionFailure("IncompleteGrant","A valid Google access token is required before checking the account.");
        using var request=new HttpRequestMessage(HttpMethod.Get,Address);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",accessToken);
        return await GoogleApiJson.ReadAsync(client??GoogleApiJson.Client,request,Project,cancellation).ConfigureAwait(false);
    }
    private static GoogleAccountDetails Project(JsonElement root)
    {
        var user=root.GetProperty("user");string permission=Text(user,"permissionId",256),email=Text(user,"emailAddress",320);if(!System.Net.Mail.MailAddress.TryCreate(email,out _))throw Invalid();
        string name=user.TryGetProperty("displayName",out var display)&&display.ValueKind==JsonValueKind.String?display.GetString()??"":"";if(name.Length>256||name.Any(char.IsControl))throw Invalid();
        long? limit=null,usage=null;if(root.TryGetProperty("storageQuota",out var quota)){limit=Bytes(quota,"limit");usage=Bytes(quota,"usage");}
        return new(new(permission,email,name),limit,usage,DateTimeOffset.UtcNow.ToString("O"));
    }
    private static string Text(JsonElement value,string key,int maximum)
    {
        string? text=value.GetProperty(key).GetString();if(string.IsNullOrWhiteSpace(text)||text.Length>maximum||text.Any(char.IsControl))throw Invalid();return text;
    }
    private static long? Bytes(JsonElement value,string key)
    {
        if(!value.TryGetProperty(key,out var bytes))return null;
        if(bytes.ValueKind!=JsonValueKind.String || !long.TryParse(bytes.GetString(),NumberStyles.None,CultureInfo.InvariantCulture,out long amount))throw Invalid();return amount;
    }
    private static GoogleConnectionFailure Invalid()=>new("InvalidGoogleResponse","Google account or storage facts are incomplete. Review the connection before using it.");
}
