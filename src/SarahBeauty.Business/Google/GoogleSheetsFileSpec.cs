using System.Text.Json;

namespace SarahBeauty.Business.Google;

internal sealed record GoogleSheetsFileObservation(string FileId,string State,string ObservedAtUtc);
internal sealed record GoogleSheetsFileSpec(string FolderId,string FolderBinding,string JobId,string GenerationId,string ManifestSha256,string Mode)
{
    internal const string Mime="application/vnd.google-apps.spreadsheet",Purpose="readable-export-v1";
    internal const string Fields=GoogleDrivePrivacy.Fields+",parents,appProperties,capabilities(canEdit)";
    internal void Validate()
    {
        GoogleDriveFolders.RequireId(FolderId);
        if(!Identity(FolderBinding)||!Identity(JobId)||!Identity(GenerationId)||ManifestSha256 is not{Length:64}||!ManifestSha256.All(char.IsAsciiHexDigit)||Mode is not("TEST" or "LIVE"))
            throw new ArgumentException("A verified readable generation, job, mode, digest, and app-folder binding are required.");
    }

    internal object Metadata()
    {
        Validate();return new{name="Sarah Beauty "+Mode+" readable "+GenerationId,mimeType=Mime,parents=new[]{FolderId},appProperties=new Dictionary<string,string>
        {
            ["sarahBeautyPurpose"]=Purpose,["sarahBeautyBinding"]=FolderBinding,["sarahBeautyExportJob"]=JobId,["sarahBeautyGeneration"]=GenerationId,
            ["sarahBeautyManifestSha256"]=ManifestSha256,["sarahBeautyMode"]=Mode,["sarahBeautyExportState"]="staging"
        }};
    }

    internal GoogleSheetsFileObservation Verify(JsonElement root,string fileId,string accountPermissionId,string? requiredState="staging")
    {
        Validate();GoogleDriveFolders.RequireId(fileId);GoogleDriveFolders.RequireId(accountPermissionId);
        if(requiredState is not(null or "staging" or "verified"))throw new ArgumentException("Choose a supported readable workbook state.");
        if(!GoogleDrivePrivacy.OnlyOwner(root,fileId,Mime,accountPermissionId)||root.GetProperty("capabilities").GetProperty("canEdit").ValueKind!=JsonValueKind.True)throw Unconfirmed();
        var parents=root.GetProperty("parents");var properties=root.GetProperty("appProperties");string? state=properties.GetProperty("sarahBeautyExportState").GetString();
        if(parents.GetArrayLength()!=1||parents[0].GetString()!=FolderId||properties.GetProperty("sarahBeautyPurpose").GetString()!=Purpose||
            properties.GetProperty("sarahBeautyBinding").GetString()!=FolderBinding||properties.GetProperty("sarahBeautyExportJob").GetString()!=JobId||
            properties.GetProperty("sarahBeautyGeneration").GetString()!=GenerationId||properties.GetProperty("sarahBeautyManifestSha256").GetString()!=ManifestSha256||
            properties.GetProperty("sarahBeautyMode").GetString()!=Mode||state is not("staging" or "verified")||requiredState is not null&&state!=requiredState)throw Unconfirmed();
        return new(fileId,state!,DateTimeOffset.UtcNow.ToString("O"));
    }

    private static bool Identity(string value)=>Guid.TryParseExact(value,"N",out var id)&&id!=Guid.Empty;
    private static GoogleConnectionFailure Unconfirmed()=>new("ReadableWorkbookNotPrivateOrBound","The workbook is not an editable private file owned only by the connected account with this job, generation, folder, and state. Review the destination before sending records.");
}
