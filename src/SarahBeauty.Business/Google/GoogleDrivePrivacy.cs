using System.Text.Json;

namespace SarahBeauty.Business.Google;

internal static class GoogleDrivePrivacy
{
    internal const string Fields="id,mimeType,ownedByMe,shared,trashed,driveId,owners(permissionId),permissions(id,type,role,deleted)";
    internal static bool OnlyOwner(JsonElement root,string id,string mime,string account)
    {
        if(root.GetProperty("id").GetString()!=id || root.GetProperty("mimeType").GetString()!=mime || root.GetProperty("ownedByMe").ValueKind!=JsonValueKind.True || root.GetProperty("shared").ValueKind!=JsonValueKind.False || root.GetProperty("trashed").ValueKind!=JsonValueKind.False || root.TryGetProperty("driveId",out _))return false;
        var owners=root.GetProperty("owners");if(owners.GetArrayLength()!=1 || owners[0].GetProperty("permissionId").GetString()!=account)return false;
        var permissions=root.GetProperty("permissions");if(permissions.GetArrayLength()!=1)return false;var permission=permissions[0];
        return permission.GetProperty("id").GetString()==account && permission.GetProperty("type").GetString()=="user" && permission.GetProperty("role").GetString()=="owner" && (!permission.TryGetProperty("deleted",out var deleted) || deleted.ValueKind==JsonValueKind.False);
    }
}
