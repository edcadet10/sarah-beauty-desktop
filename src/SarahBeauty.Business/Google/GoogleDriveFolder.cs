using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SarahBeauty.Business.Google;

internal sealed record GoogleDriveFolder(string Id,string Name,string Binding,string ObservedAtUtc);
internal sealed class GoogleDriveFolders(HttpClient? client=null)
{
    private const string Root="https://www.googleapis.com/drive/v3/files";
    private const string Mime="application/vnd.google-apps.folder";
    private const string Purpose="private-output-v1";
    private const string Fields="id,name,mimeType,ownedByMe,shared,trashed,driveId,owners(permissionId),permissions(id,type,role,deleted),appProperties,capabilities(canAddChildren)";
    internal async Task<GoogleDriveFolder> CreateAsync(string accessToken,string accountPermissionId,string binding,string mode,CancellationToken cancellation=default)
    {
        RequireId(accountPermissionId);RequireBinding(binding);if(mode is not("TEST" or "LIVE"))throw new ArgumentException("Choose a supported business mode.");
        using var request=Request(HttpMethod.Post,Root+"?ignoreDefaultVisibility=true&fields=id",accessToken);
        request.Content=JsonContent.Create(new{name="Sarah Beauty "+mode+" exports and archives",mimeType=Mime,appProperties=new Dictionary<string,string>{{"sarahBeautyPurpose",Purpose},{"sarahBeautyBinding",binding}}});
        string id=await GoogleApiJson.ReadAsync(client??GoogleApiJson.Client,request,root=>{string value=root.GetProperty("id").GetString()??"";RequireId(value);return value;},cancellation).ConfigureAwait(false);
        return await VerifyAsync(accessToken,id,accountPermissionId,binding,cancellation).ConfigureAwait(false);
    }
    internal async Task<GoogleDriveFolder> VerifyAsync(string accessToken,string folderId,string accountPermissionId,string binding,CancellationToken cancellation=default)
    {
        RequireId(folderId);RequireId(accountPermissionId);RequireBinding(binding);
        using var request=Request(HttpMethod.Get,Root+"/"+folderId+"?fields="+Uri.EscapeDataString(Fields),accessToken);
        return await GoogleApiJson.ReadAsync(client??GoogleApiJson.Client,request,root=>Project(root,folderId,accountPermissionId,binding),cancellation).ConfigureAwait(false);
    }
    private static GoogleDriveFolder Project(JsonElement root,string id,string account,string binding)
    {
        if(Text(root,"id")!=id || Text(root,"mimeType")!=Mime || !Boolean(root,"ownedByMe",true) || !Boolean(root,"shared",false) || !Boolean(root,"trashed",false) || root.TryGetProperty("driveId",out _))throw NotPrivate();
        var properties=root.GetProperty("appProperties");if(Text(properties,"sarahBeautyPurpose")!=Purpose || Text(properties,"sarahBeautyBinding")!=binding || !Boolean(root.GetProperty("capabilities"),"canAddChildren",true))throw NotPrivate();
        var owners=root.GetProperty("owners");if(owners.GetArrayLength()!=1 || Text(owners[0],"permissionId")!=account)throw NotPrivate();
        var permissions=root.GetProperty("permissions");if(permissions.GetArrayLength()!=1)throw NotPrivate();var permission=permissions[0];
        if(Text(permission,"id")!=account || Text(permission,"type")!="user" || Text(permission,"role")!="owner" || (permission.TryGetProperty("deleted",out var deleted) && deleted.ValueKind!=JsonValueKind.False))throw NotPrivate();
        string name=Text(root,"name");if(string.IsNullOrWhiteSpace(name)||name.Length>256||name.Any(char.IsControl))throw NotPrivate();return new(id,name,binding,DateTimeOffset.UtcNow.ToString("O"));
    }
    private static string Text(JsonElement value,string key)=>value.GetProperty(key).GetString()??throw NotPrivate();
    private static bool Boolean(JsonElement value,string key,bool expected)=>value.GetProperty(key).ValueKind==(expected?JsonValueKind.True:JsonValueKind.False);
    internal static HttpRequestMessage Request(HttpMethod method,string address,string token)
    {
        if(string.IsNullOrWhiteSpace(token)||token.Length>16384||token.Any(c=>char.IsWhiteSpace(c)||char.IsControl(c)))throw new GoogleConnectionFailure("IncompleteGrant","A valid Google access token is required.");
        var request=new HttpRequestMessage(method,address);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);return request;
    }
    internal static void RequireId(string value)
    {
        if(string.IsNullOrEmpty(value)||value.Length>256||value.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not('_' or '-')))throw new GoogleConnectionFailure("InvalidGoogleFileIdentity","The Google file or account identity is unsupported. Review the destination.");
    }
    private static void RequireBinding(string binding)
    {
        if(!Guid.TryParseExact(binding,"N",out var value)||value==Guid.Empty)throw new ArgumentException("A new output-folder identity is required.");
    }
    private static GoogleConnectionFailure NotPrivate()=>new("GoogleFolderNotPrivate","The Google folder is not a writable, app-bound private folder owned only by the connected account. Review its access before sending records.");
}
