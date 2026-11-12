using System.Numerics;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal sealed record AssetAccountValue(string AccountId,string Code,long Cents);
internal sealed record AssetBookValue(List<AssetAccountValue> Costs,long Depreciation,bool HasHistory)
{
    internal long Cost=>Cents(Costs.Aggregate(BigInteger.Zero,(sum,value)=>sum+value.Cents));
    internal long Carrying=>checked(Cost-Depreciation);

    internal static AssetBookValue Read(DbWork db,OwnerSession owner,string asset,DateOnly date)=>Read(db,owner.BusinessId,asset,date);
    internal static AssetBookValue Read(DbWork db,string businessId,string asset,DateOnly date)
    {
        var lines=db.Read("""
            SELECT A.Id,A.Code,J.DebitCents,J.CreditCents FROM Movements M JOIN JournalLines J ON J.MovementId=M.Id
            JOIN Accounts A ON A.Id=J.AccountId WHERE M.BusinessId=$business AND J.AssetId=$asset AND M.Date<=$date
            AND A.Type='Asset' AND A.CashKind='None';
            """,r=>(AccountId:r.GetString(0),Code:r.GetString(1),Debit:r.GetInt64(2),Credit:r.GetInt64(3)),("$business",businessId),("$asset",asset),("$date",date.ToStorageDate()));
        var values=lines.GroupBy(v=>(v.AccountId,v.Code)).Select(group=>new AssetAccountValue(group.Key.AccountId,group.Key.Code,Cents(group.Aggregate(BigInteger.Zero,(sum,line)=>sum+(BigInteger)line.Debit-line.Credit)))).ToList();
        var result=new AssetBookValue(values.Where(v=>v.Code!="1590").ToList(),Cents(-values.Where(v=>v.Code=="1590").Aggregate(BigInteger.Zero,(sum,value)=>sum+value.Cents)),values.Count>0);
        if(!result.HasHistory || result.Costs.Any(c=>c.Cents<0) || result.Depreciation<0 || result.Depreciation>result.Cost)
            throw new ArgumentException("Review the asset's approved book value and accumulated depreciation. Unknown value is not zero.");
        return result;
    }

    private static long Cents(BigInteger value)
    {
        try{return checked((long)value);}
        catch(OverflowException){throw new ArgumentException("The asset book value exceeds the supported cent range.");}
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
