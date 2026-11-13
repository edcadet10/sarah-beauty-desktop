using System.Globalization;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Ownership;

internal static class OwnerInterestQueries
{
    internal static IReadOnlyList<OwnerInterestOwner> Owners(DbWork db,string business)=>db.Read("SELECT Id,Name,Active FROM Owners WHERE BusinessId=$business ORDER BY Name,Id;",
        r=>new OwnerInterestOwner(r.GetString(0),r.GetString(1),r.GetInt32(2)==1),("$business",business)).AsReadOnly();
    internal static IReadOnlyList<OwnerInterestVersion> Read(DbWork db,string business)
    {
        if(Convert.ToInt64(db.Scalar("SELECT count(*) FROM OwnerInterestShares s LEFT JOIN OwnerInterestVersions v ON v.Id=s.VersionId AND v.BusinessId=s.BusinessId WHERE s.BusinessId=$business AND v.Id IS NULL;",("$business",business)))!=0)throw OwnerInterestCodec.Invalid();
        var owners=Owners(db,business).Select(o=>o.Id).ToHashSet(StringComparer.Ordinal);
        var values=db.Read("""
            SELECT Id,Revision,EffectiveFrom,SupersedesId,RecordedByOwnerId,RecordedAtUtc,EvidenceVersionId,SnapshotJson,SnapshotHash
            FROM OwnerInterestVersions WHERE BusinessId=$business ORDER BY Revision;
            """,r=>
            {
                var value=OwnerInterestCodec.Read(r.GetString(7),r.GetString(8));
                if(value.BusinessId!=business || value.Id!=r.GetString(0) || value.Revision!=r.GetInt64(1) || value.EffectiveFrom.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)!=r.GetString(2) ||
                    value.SupersedesId!=(r.IsDBNull(3)?null:r.GetString(3)) || value.RecordedByOwnerId!=r.GetString(4) || value.RecordedAtUtc!=r.GetString(5) || value.EvidenceVersionId!=(r.IsDBNull(6)?null:r.GetString(6)))throw OwnerInterestCodec.Invalid();
                return value;
            },("$business",business));
        for(int i=0;i<values.Count;i++)
        {
            var value=values[i];if(value.Revision!=i+1L || !owners.Contains(value.RecordedByOwnerId) || value.Shares.Any(s=>!owners.Contains(s.OwnerId)))throw OwnerInterestCodec.Invalid();
            CheckShares(db,value);CheckCorrection(values.Take(i),value);
        }
        return values.AsReadOnly();
    }
    private static void CheckShares(DbWork db,OwnerInterestVersion value)
    {
        var shares=db.Read("SELECT OwnerId,OwnerNameAtConfirmation,FractionText FROM OwnerInterestShares WHERE VersionId=$version AND BusinessId=$business ORDER BY OwnerId;",
            r=>(Id:r.GetString(0),Name:r.GetString(1),Fraction:r.GetString(2)),("$version",value.Id),("$business",value.BusinessId));
        if(shares.Count!=value.Shares.Count || shares.Where((row,i)=>row.Id!=value.Shares[i].OwnerId || row.Name!=value.Shares[i].OwnerName || row.Fraction!=value.Shares[i].Fraction.ToString("0.######",CultureInfo.InvariantCulture)).Any())throw OwnerInterestCodec.Invalid();
    }
    private static void CheckCorrection(IEnumerable<OwnerInterestVersion> earlier,OwnerInterestVersion value)
    {
        if(value.SupersedesId is null){if(earlier.Any(v=>v.EffectiveFrom==value.EffectiveFrom))throw OwnerInterestCodec.Invalid();return;}
        var prior=earlier.SingleOrDefault(v=>v.Id==value.SupersedesId);
        if(prior is null || prior.EffectiveFrom!=value.EffectiveFrom || earlier.Any(v=>v.SupersedesId==prior.Id))throw OwnerInterestCodec.Invalid();
    }
}
