using System.Text.Json;
using System.Text.Json.Nodes;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsFileSpecTests
{
    private const string FileId="fictional-workbook",Account="fictional-account";
    private static GoogleSheetsFileSpec Spec()=>new("fictional-folder","00000000000000000000000000000001","00000000000000000000000000000002","00000000000000000000000000000003",new string('A',64),"TEST");

    [Fact]
    public void MetadataContainsOnlyGenericCreationIdentityAndPrivateVerificationDefaultsToStaging()
    {
        var spec=Spec();var metadata=JsonSerializer.SerializeToElement(spec.Metadata());Assert.Equal(4,metadata.EnumerateObject().Count());
        Assert.False(metadata.TryGetProperty("id",out _));Assert.False(metadata.TryGetProperty("permissions",out _));
        Assert.Equal(GoogleSheetsFileSpec.Mime,metadata.GetProperty("mimeType").GetString());Assert.Equal(spec.FolderId,metadata.GetProperty("parents")[0].GetString());
        var facts=Facts(spec);Assert.Equal("staging",spec.Verify(JsonSerializer.SerializeToElement(facts),FileId,Account).State);
        facts["appProperties"]!["sarahBeautyExportState"]="verified";
        Assert.Throws<GoogleConnectionFailure>(()=>spec.Verify(JsonSerializer.SerializeToElement(facts),FileId,Account));
        Assert.Equal("verified",spec.Verify(JsonSerializer.SerializeToElement(facts),FileId,Account,"verified").State);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("mime")]
    [InlineData("shared")]
    [InlineData("domain")]
    [InlineData("group")]
    [InlineData("owner")]
    [InlineData("owned")]
    [InlineData("shared drive")]
    [InlineData("trashed")]
    [InlineData("parent")]
    [InlineData("extra parent")]
    [InlineData("binding")]
    [InlineData("job")]
    [InlineData("generation")]
    [InlineData("digest")]
    [InlineData("mode")]
    [InlineData("purpose")]
    [InlineData("read only")]
    [InlineData("state")]
    public void ForeignSharedReadOnlyOrMisboundFilesCannotPassTheExpectedGeneration(string defect)
    {
        var spec=Spec();var facts=Facts(spec);var properties=facts["appProperties"]!;
        switch(defect)
        {
            case "id":facts["id"]="other-file";break;case "mime":facts["mimeType"]="application/octet-stream";break;
            case "shared":facts["shared"]=true;break;case "domain":case "group":facts["permissions"]!.AsArray().Add(new JsonObject{["id"]="other-permission",["type"]=defect,["role"]="reader"});break;
            case "owner":facts["owners"]![0]!["permissionId"]="other-account";break;case "owned":facts["ownedByMe"]=false;break;
            case "shared drive":facts["driveId"]="other-drive";break;case "trashed":facts["trashed"]=true;break;
            case "parent":facts["parents"]![0]="other-folder";break;case "extra parent":facts["parents"]!.AsArray().Add("other-folder");break;
            case "binding":properties["sarahBeautyBinding"]=Guid.NewGuid().ToString("N");break;case "job":properties["sarahBeautyExportJob"]=Guid.NewGuid().ToString("N");break;
            case "generation":properties["sarahBeautyGeneration"]=Guid.NewGuid().ToString("N");break;case "digest":properties["sarahBeautyManifestSha256"]=new string('B',64);break;
            case "mode":properties["sarahBeautyMode"]="LIVE";break;case "purpose":properties["sarahBeautyPurpose"]="other-purpose";break;
            case "read only":facts["capabilities"]!["canEdit"]=false;break;case "state":properties["sarahBeautyExportState"]="unknown";break;
        }
        var error=Assert.Throws<GoogleConnectionFailure>(()=>spec.Verify(JsonSerializer.SerializeToElement(facts),FileId,Account));
        Assert.Equal("ReadableWorkbookNotPrivateOrBound",error.Code);Assert.DoesNotContain("fictional",error.ToString());
    }

    private static JsonObject Facts(GoogleSheetsFileSpec spec)=>new()
    {
        ["id"]=FileId,["mimeType"]=GoogleSheetsFileSpec.Mime,["ownedByMe"]=true,["shared"]=false,["trashed"]=false,
        ["owners"]=new JsonArray(new JsonObject{["permissionId"]=Account}),["permissions"]=new JsonArray(new JsonObject{["id"]=Account,["type"]="user",["role"]="owner",["deleted"]=false}),
        ["parents"]=new JsonArray(spec.FolderId),["capabilities"]=new JsonObject{["canEdit"]=true},
        ["appProperties"]=JsonSerializer.SerializeToNode(spec.Metadata())!["appProperties"]!.DeepClone()
    };
}
