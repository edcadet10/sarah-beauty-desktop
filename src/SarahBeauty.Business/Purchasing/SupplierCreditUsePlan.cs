using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Purchasing;

internal static class SupplierCreditUsePlan
{
    internal static (Purchase Source,Purchase Target,List<PurchaseSettlement> Out,List<PurchaseSettlement> In) Build(DbWork db,BusinessStore store,OwnerSession owner,FinancialDraft draft)
    {
        var original=CorrectionRules.Original(db,owner,draft.OriginalId);
        var records=new RecordStore<Purchase>(store,"Purchase");
        var source=records.Get(db,owner,original.PurchaseId);
        var target=records.Get(db,owner,draft.PurchaseId);
        var outgoing=SupplierCreditSource.Rows(db,store,owner,draft);
        var incoming=PurchasePaymentPlan.Rows(db,store,owner,target,draft);
        long targetBusiness=incoming.Sum(r=>r.BusinessCents);
        long draw=draft.PurchaseFunding=="Business cash"?draft.AmountCents-targetBusiness:
            draft.SupplierCreditTreatment=="Owner withdrawal"?Math.Max(0,outgoing.Sum(r=>checked(r.BusinessCents+r.DrawCents))-targetBusiness):0;
        long[] draws=draw==0?new long[incoming.Count]:PaymentPlan.Allocate(draw,incoming.Select(r=>r.Cents-r.BusinessCents).ToArray());
        incoming=incoming.Select((r,i)=>r with{DrawCents=draws[i]}).ToList();
        return(source,target,outgoing,incoming);
    }

    internal static List<JournalLine> Lines(DbWork db,BusinessStore store,OwnerSession owner,FinancialDraft draft,List<Account> accounts)
    {
        var plan=Build(db,store,owner,draft);
        if(draft.AssetAdjustments.Where(c=>c.Treatment==AssetValueChoice.Retire).Any(c=>plan.In.Any(r=>r.BusinessCents>0 && plan.Target.Lines.Single(l=>l.Id==r.LineId).AssetId==c.AssetId)))
            throw new ArgumentException("A returned asset cannot also receive the replacement purchase value. Use the replacement asset's own record.");
        var lines=PurchasePaymentPlan.BusinessLines(db,store,owner,plan.Target,draft,accounts,plan.In);
        lines.AddRange(PurchaseCostReturns.Lines(db,owner,plan.Source,draft,accounts,plan.Out));
        long drawChange=plan.In.Sum(r=>r.DrawCents)-plan.Out.Sum(r=>r.DrawCents);
        long fundingChange=plan.In.Sum(r=>checked(r.BusinessCents+r.DrawCents))-plan.Out.Sum(r=>checked(r.BusinessCents+r.DrawCents));
        if((drawChange!=0 || fundingChange!=0) && db.Scalar("SELECT Id FROM Owners WHERE Id=$id AND BusinessId=$business AND Active=1;",
            ("$id",draft.EconomicOwnerId),("$business",owner.BusinessId)) is null)throw new ArgumentException("Choose the owner whose private or funding share changes.");
        string Code(string code)=>accounts.Single(a=>a.Code==code && a.Active).Id;
        if(drawChange!=0)lines.Add(new(Code("3100"),Math.Max(0,drawChange),Math.Max(0,-drawChange)));
        if(fundingChange!=0)
        {
            if(draft.PurchaseFunding=="Business cash")throw new ArgumentException("Review the original cash-funded personal shares before using this credit.");
            lines.Add(new(Code(draft.PurchaseFunding=="Owner contribution"?"3000":"2100"),Math.Max(0,-fundingChange),Math.Max(0,fundingChange)));
        }
        return lines;
    }
}
