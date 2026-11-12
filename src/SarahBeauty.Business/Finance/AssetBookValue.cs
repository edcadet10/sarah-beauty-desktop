using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal sealed record AssetAccountValue(string AccountId,string Code,long Cents);
internal sealed record AssetBookValue(List<AssetAccountValue> Costs,long Depreciation,bool HasHistory)
{
    internal long Cost=>Costs.Sum(c=>c.Cents);
    internal long Carrying=>checked(Cost-Depreciation);

    internal static AssetBookValue Read(DbWork db,OwnerSession owner,string asset,DateOnly date)=>Read(db,owner.BusinessId,asset,date);
    internal static AssetBookValue Read(DbWork db,string businessId,string asset,DateOnly date)
    {
        var values=db.Read("""
            SELECT A.Id,A.Code,SUM(J.DebitCents-J.CreditCents) FROM Movements M JOIN JournalLines J ON J.MovementId=M.Id
            JOIN Accounts A ON A.Id=J.AccountId WHERE M.BusinessId=$business AND J.AssetId=$asset AND M.Date<=$date
            AND A.Type='Asset' AND A.CashKind='None' GROUP BY A.Id,A.Code;
            """,r=>new AssetAccountValue(r.GetString(0),r.GetString(1),r.GetInt64(2)),("$business",businessId),("$asset",asset),("$date",date.ToStorageDate()));
        var result=new AssetBookValue(values.Where(v=>v.Code!="1590").ToList(),-values.Where(v=>v.Code=="1590").Sum(v=>v.Cents),values.Count>0);
        if(!result.HasHistory || result.Costs.Any(c=>c.Cents<0) || result.Depreciation<0 || result.Depreciation>result.Cost)
            throw new ArgumentException("Review the asset's approved book value and accumulated depreciation. Unknown value is not zero.");
        return result;
    }

    internal List<JournalLine> Reduce(long cents,string asset)
    {
        if(cents<0 || cents>Cost)throw new ArgumentException("The reduction exceeds the asset's approved book cost.");
        if(cents==0)return [];
        var costs=Costs.Where(c=>c.Cents>0).ToList();
        long[] shares=Rules.PaymentPlan.Allocate(cents,costs.Select(c=>c.Cents).ToArray());
        return costs.Select((c,i)=>new JournalLine(c.AccountId,0,shares[i],asset)).Where(l=>l.CreditCents>0).ToList();
    }
}
