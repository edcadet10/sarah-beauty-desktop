using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class AssetRetirements
{
    internal static void CheckLaterMovement(DbWork db,OwnerSession owner,string asset,DateOnly date)
    {
        if(db.Scalar("SELECT M.Id FROM Movements M JOIN JournalLines J ON J.MovementId=M.Id WHERE M.BusinessId=$business AND J.AssetId=$asset AND M.Date>$date LIMIT 1;",
            ("$business",owner.BusinessId),("$asset",asset),("$date",date.ToString("yyyy-MM-dd"))) is not null)
            throw new ArgumentException("This retirement date precedes another asset movement. Review the asset history first.");
    }

    internal static void ValidateReversal(DbWork db,OwnerSession owner,string original,DateOnly date)
    {
        var assets=db.Read("SELECT Id FROM Records WHERE BusinessId=$business AND Kind='Asset' AND json_extract(DataJson,'$.DisposalMovementId')=$id;",
            r=>r.GetString(0),("$business",owner.BusinessId),("$id",original));
        foreach(string asset in assets)
            if(db.Scalar("""
                SELECT M.Id FROM Movements M JOIN JournalLines J ON J.MovementId=M.Id WHERE M.BusinessId=$business AND J.AssetId=$asset
                AND M.rowid>(SELECT rowid FROM Movements WHERE Id=$original) AND M.Kind<>'Reversal'
                AND NOT EXISTS(SELECT 1 FROM Movements R WHERE R.ReversesId=M.Id AND R.Date<=$date) LIMIT 1;
                """,("$business",owner.BusinessId),("$asset",asset),("$original",original),("$date",date.ToString("yyyy-MM-dd"))) is not null)
                throw new ArgumentException("Reverse or reclassify later asset recovery movements before reversing this retirement.");
    }

    internal static void AfterPosting(DbWork db,BusinessStore store,OwnerSession owner,FinancialDraft draft,string movement)
    {
        var records=new RecordStore<Asset>(store,"Asset");
        if(draft.Kind==FinancialKind.Reversal)
        {
            foreach(var asset in records.List(db,owner).Where(a=>a.DisposalMovementId==draft.OriginalId))
                records.Save(db,owner,asset with{DisposedDate=null,DisposalMovementId=""});
            return;
        }
        foreach(var choice in draft.AssetAdjustments.Where(c=>c.Treatment==AssetValueChoice.Retire))
        {
            var asset=records.Get(db,owner,choice.AssetId);
            if(asset.DisposedDate.HasValue)throw new ArgumentException("This asset already has a retirement. Review its linked correction history.");
            records.Save(db,owner,asset with{DisposedDate=draft.Date,DisposalMovementId=movement});
        }
    }
}
