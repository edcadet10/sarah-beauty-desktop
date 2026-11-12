using System.Text.Json;

namespace SarahBeauty.Business.Google;

internal sealed class GoogleDesktopClient(string clientId,string secret,string projectId)
{
    internal const int MaximumFileBytes=65536;
    internal string ClientId {get;}=clientId;
    internal string ClientSecret {get;}=secret;
    internal string ProjectId {get;}=projectId;
    public override string ToString()=>"Google desktop client (credentials hidden)";
    internal static GoogleDesktopClient Parse(byte[] json)
    {
        if(json.Length is <1 or >MaximumFileBytes)throw Invalid();
        try
        {
            using var document=JsonDocument.Parse(json,new(){MaxDepth=8});var root=document.RootElement;
            Unique(root);if(root.EnumerateObject().Count()!=1 || !root.TryGetProperty("installed",out var installed))throw Invalid();Unique(installed);
            string id=Text(installed,"client_id",512),secret=Text(installed,"client_secret",512),project=Text(installed,"project_id",128);
            if(!id.EndsWith(".apps.googleusercontent.com",StringComparison.Ordinal) || id.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c is '-' or '_' or '.')))throw Invalid();
            string authorization=Text(installed,"auth_uri",256);if(authorization is not("https://accounts.google.com/o/oauth2/auth" or "https://accounts.google.com/o/oauth2/v2/auth") || Text(installed,"token_uri",256)!="https://oauth2.googleapis.com/token")throw Invalid();
            return new(id,secret,project);
        }
        catch(Exception error) when(error is JsonException or InvalidOperationException or KeyNotFoundException){throw Invalid();}
    }
    private static void Unique(JsonElement value)
    {
        if(value.ValueKind!=JsonValueKind.Object)throw Invalid();var names=new HashSet<string>(StringComparer.Ordinal);foreach(var property in value.EnumerateObject())if(!names.Add(property.Name))throw Invalid();
    }
    private static string Text(JsonElement value,string key,int maximum)
    {
        string? text=value.GetProperty(key).GetString();if(string.IsNullOrWhiteSpace(text)||text.Length>maximum||text.Any(char.IsWhiteSpace)||text.Any(char.IsControl))throw Invalid();return text;
    }
    private static InvalidDataException Invalid()=>new("Choose the downloaded JSON for this app's Google Desktop OAuth client. Web, service-account, changed-endpoint, duplicate, and incomplete configurations are not supported.");
}
