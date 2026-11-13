using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Setup;

internal static class BusinessProfileQueries
{
    internal static IReadOnlyList<BusinessProfileVersion> Read(DbWork db,string business)
    {
        var values=db.Read("""
            SELECT Id,Revision,RequestId,SnapshotJson,SnapshotHash,PayloadHash,RecordedByOwnerId,RecordedAtUtc
            FROM BusinessProfileVersions WHERE BusinessId=$business ORDER BY Revision;
            """,r=>
            {
                var value=BusinessProfileCodec.Read(r.GetString(3),r.GetString(4));
                if(value.Id!=r.GetString(0)||value.BusinessId!=business||value.Revision!=r.GetInt64(1)||value.RequestId!=r.GetString(2)||BusinessProfileCodec.Payload(BusinessProfileCodec.Change(value))!=r.GetString(5)||value.RecordedByOwnerId!=r.GetString(6)||value.RecordedAtUtc!=r.GetString(7))throw BusinessProfileCodec.Invalid();return value;
            },("$business",business));
        string? mode=db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';") as string;var requests=new HashSet<string>(StringComparer.Ordinal);
        for(int i=0;i<values.Count;i++)
        {
            var value=values[i];if(value.Revision!=i+1L||!requests.Add(value.RequestId)||value.Mode!=mode||db.Scalar("SELECT Id FROM Owners WHERE Id=$owner AND BusinessId=$business;",("$owner",value.RecordedByOwnerId),("$business",business)) is null)throw BusinessProfileCodec.Invalid();CheckEvidence(db,value);
        }
        if(values.Count>0&&values[^1].Profile.DisplayName!=(string?)db.Scalar("SELECT DisplayName FROM Business WHERE Id=$id;",("$id",business)))throw BusinessProfileCodec.Invalid();return values.AsReadOnly();
    }
    private static void CheckEvidence(DbWork db,BusinessProfileVersion value)
    {
        foreach(var review in new[]{value.Profile.Formation,value.Profile.EinReview,value.Profile.Banking,value.Profile.Tax})
        {
            if(review.Evidence is not{} evidence)continue;var file=AttachmentQueries.List(db,value.BusinessId,versionId:evidence.VersionId).SingleOrDefault();
            if(file is null||file.Version.Category=="Customer photo"||ManagedEvidenceFile.From(file)!=evidence)throw BusinessProfileCodec.Invalid();
        }
    }
}
