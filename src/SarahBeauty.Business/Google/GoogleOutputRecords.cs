using System.Text.Json;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed record GoogleOutputPolicy(int Version,string Id,string BusinessId,string InstanceId,string ConnectionId,int Revision,string FolderId,string FolderName,string FolderBinding,bool DriveArchivesEnabled,bool SheetsExportEnabled,string ConfirmedBy,string ConfirmedAtUtc,string FolderVerifiedAtUtc);
public sealed record GoogleOutputDraft(bool DriveArchivesEnabled,bool SheetsExportEnabled,int ExpectedRevision);
internal enum GoogleOutputKind{DriveArchive,SheetsExport}
internal sealed record GoogleOutputPermit(GoogleOutputPolicy Policy,GoogleConnectionRecord Connection,GoogleOutputKind Kind);
internal static class GoogleOutputRecords
{
    private static string Key(string business,string instance)=>"GoogleOutputPolicy/"+business+"/"+instance;
    internal static GoogleOutputPolicy? Read(DbWork db,string business,string instance)
    {
        string? json=db.Scalar("SELECT Value FROM AppMetadata WHERE Key=$key;",("$key",Key(business,instance))) as string;if(json is null)return null;
        var value=JsonSerializer.Deserialize<GoogleOutputPolicy>(json)??throw Invalid();
        if(value.Version!=1 || value.BusinessId!=business || value.InstanceId!=instance || value.Revision<1 || !Guid.TryParse(value.Id,out _) || !Guid.TryParse(value.ConnectionId,out _) || !Guid.TryParse(value.ConfirmedBy,out _) || !Guid.TryParseExact(value.FolderBinding,"N",out var binding) || binding==Guid.Empty || string.IsNullOrWhiteSpace(value.FolderName) || value.FolderName.Length>256 || value.FolderName.Any(char.IsControl) || !DateTimeOffset.TryParse(value.ConfirmedAtUtc,out _) || !DateTimeOffset.TryParse(value.FolderVerifiedAtUtc,out _))throw Invalid();GoogleDriveFolders.RequireId(value.FolderId);return value;
    }
    internal static void RequireRevision(GoogleOutputPolicy? actual,int expected){if((actual?.Revision??0)!=expected)throw new ArgumentException("Google output settings changed. Reload them before saving.");}
    internal static void Save(DbWork db,GoogleOutputPolicy value)=>db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$json) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value;",("$key",Key(value.BusinessId,value.InstanceId)),("$json",JsonSerializer.Serialize(value)));
    private static InvalidDataException Invalid()=>new("The saved Google output settings need review.");
}
