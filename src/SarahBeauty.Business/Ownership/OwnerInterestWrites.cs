using System.Globalization;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Ownership;

internal static class OwnerInterestWrites
{
    internal static void Insert(DbWork db,OwnerInterestVersion value,string json,string hash)
    {
        db.Execute("""
            INSERT INTO OwnerInterestVersions(Id,BusinessId,Revision,EffectiveFrom,SupersedesId,RecordedByOwnerId,RecordedAtUtc,EvidenceVersionId,SnapshotJson,SnapshotHash)
            VALUES($id,$business,$revision,$date,$supersedes,$owner,$at,$evidence,$json,$hash);
            """,("$id",value.Id),("$business",value.BusinessId),("$revision",value.Revision),("$date",value.EffectiveFrom.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)),
            ("$supersedes",value.SupersedesId),("$owner",value.RecordedByOwnerId),("$at",value.RecordedAtUtc),("$evidence",value.EvidenceVersionId),("$json",json),("$hash",hash));
        foreach(var share in value.Shares)db.Execute("""
            INSERT INTO OwnerInterestShares(VersionId,BusinessId,OwnerId,OwnerNameAtConfirmation,FractionText) VALUES($version,$business,$owner,$name,$fraction);
            """,("$version",value.Id),("$business",value.BusinessId),("$owner",share.OwnerId),("$name",share.OwnerName),("$fraction",share.Fraction.ToString("0.######",CultureInfo.InvariantCulture)));
    }
}
