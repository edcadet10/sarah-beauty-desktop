using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Purchasing;

internal static class PurchaseCostReturns
{
    internal static List<JournalLine> Lines(DbWork db,OwnerSession owner,Purchase purchase,FinancialDraft draft,List<Account> accounts,List<PurchaseSettlement> rows)
    {
        var costs=rows.Where(r=>r.BusinessCents>0).Select(r=>(Line:purchase.Lines.Single(l=>l.Id==r.LineId),Cents:r.BusinessCents)).ToList();
        if(draft.AssetAdjustments.Any(c=>!costs.Any(v=>v.Line.AssetId==c.AssetId)))
            throw new ArgumentException("Each asset decision must belong to a business asset included in this supplier refund or credit use.");
        var lines=new List<JournalLine>();
        foreach(var group in costs.GroupBy(c=>c.Line.AssetId))
        {
            var choice=draft.AssetAdjustments.SingleOrDefault(c=>c.AssetId==group.Key);
            if(choice is not null && choice.Treatment!=AssetValueChoice.Reduce)
                lines.AddRange(AssetRefunds.ValueLines(db,owner,draft,group.Key,group.Sum(c=>c.Cents),choice,accounts));
            else lines.AddRange(group.Select(c=>new JournalLine(c.Line.AccountId,0,c.Cents,c.Line.AssetId)));
        }
        return lines;
    }
}
