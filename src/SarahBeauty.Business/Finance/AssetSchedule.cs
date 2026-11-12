using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class AssetSchedule
{
    internal static AssetScheduleSnapshot Read(DbWork db,BusinessStore store,OwnerSession owner,DateOnly from,DateOnly through)=>Read(db,owner.BusinessId,from,through);
    internal static AssetScheduleSnapshot Read(DbWork db,string businessId,DateOnly from,DateOnly through)
    {
        var assets=RecordStore<Asset>.ReadBusiness(db,businessId,"Asset");
        var entries=Entries(db,businessId,through);
        var groups=entries.ToLookup(e=>e.AssetId);
        var positions=assets.Select(a=>Position(db,businessId,a,groups[a.Id].ToList(),from,through)).ToList();
        return new(1,positions,entries,Events(db,businessId,through));
    }

    private static AssetSchedulePosition Position(DbWork db,string businessId,Asset asset,List<AssetScheduleEntry> entries,DateOnly from,DateOnly through)
    {
        var bookEntries=entries.Where(e=>e.IsBookValue).ToList();
        var period=bookEntries.Where(e=>e.Date>=from).ToList();
        var costs=period.Where(e=>e.AccountCode!="1590").ToList();
        var depreciation=period.Where(e=>e.AccountCode=="1590").ToList();
        var opening=bookEntries.Where(e=>e.Date<from).ToList();
        var closing=bookEntries.Count>0?AssetBookValue.Read(db,businessId,asset.Id,through):null;
        return new(Facts(asset),closing is null?null:opening.Where(e=>e.AccountCode!="1590").Sum(e=>e.DebitCents-e.CreditCents),
            closing is null?null:opening.Where(e=>e.AccountCode=="1590").Sum(e=>e.CreditCents-e.DebitCents),
            costs.Sum(e=>e.DebitCents),costs.Sum(e=>e.CreditCents),depreciation.Sum(e=>e.CreditCents),depreciation.Sum(e=>e.DebitCents),
            closing?.Cost,closing?.Depreciation,closing?.Carrying,AssetDisposals.IsDisposed(db,businessId,asset.Id,through));
    }

    private static AssetRegisterFacts Facts(Asset a)=>new(a.Id,a.Name,a.Archived,a.LegalOwner,a.AcquisitionDate,a.InServiceDate,
        a.CostCents,a.TaxBasisCents,a.BusinessUseFraction,a.Funding,a.EvidenceId,a.Revision);

    private static List<AssetScheduleEntry> Entries(DbWork db,string businessId,DateOnly through)=>db.Read("""
        SELECT COALESCE(NULLIF(J.AssetId,''),M.AssetId),M.Id,M.Date,M.Kind,M.Memo,A.Code,A.Name,J.DebitCents,J.CreditCents,
            CASE WHEN J.AssetId<>'' AND A.Type='Asset' AND A.CashKind='None' THEN 1 ELSE 0 END
        FROM Movements M JOIN JournalLines J ON J.MovementId=M.Id JOIN Accounts A ON A.Id=J.AccountId
        WHERE M.BusinessId=$business AND M.Date<=$through AND COALESCE(NULLIF(J.AssetId,''),M.AssetId,'')<>''
        ORDER BY M.Date,M.rowid,J.rowid;
        """,r=>new AssetScheduleEntry(r.GetString(0),r.GetString(1),DateOnly.Parse(r.GetString(2)),r.GetString(3),r.GetString(4),
            r.GetString(5),r.GetString(6),r.GetInt64(7),r.GetInt64(8),r.GetInt32(9)==1),
        ("$business",businessId),("$through",through.ToString("yyyy-MM-dd")));

    private static List<AssetScheduleEvent> Events(DbWork db,string businessId,DateOnly through)=>db.Read("""
        SELECT A.Id,M.Id,M.Date,M.Kind,M.Memo FROM Records A JOIN Movements M ON M.BusinessId=A.BusinessId
        WHERE A.BusinessId=$business AND A.Kind='Asset' AND M.Date<=$through
        AND (M.AssetId=A.Id OR EXISTS(SELECT 1 FROM JournalLines J WHERE J.MovementId=M.Id AND J.AssetId=A.Id)
            OR EXISTS(SELECT 1 FROM json_each(M.DataJson,'$.AssetAdjustments') C WHERE json_extract(C.value,'$.AssetId')=A.Id))
        ORDER BY M.Date,M.rowid,A.Id;
        """,r=>new AssetScheduleEvent(r.GetString(0),r.GetString(1),DateOnly.Parse(r.GetString(2)),r.GetString(3),r.GetString(4)),
        ("$business",businessId),("$through",through.ToString("yyyy-MM-dd")));
}
