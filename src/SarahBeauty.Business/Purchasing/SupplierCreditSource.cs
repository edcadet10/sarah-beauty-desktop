using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Purchasing;

internal static class SupplierCreditSource
{
    internal static FinancialDraft Prepare(DbWork db,BusinessStore store,OwnerSession owner,FinancialDraft draft)
    {
        var original=CorrectionRules.Original(db,owner,draft.OriginalId);
        if(original.Kind is not (FinancialKind.PurchasePayment or FinancialKind.SupplierCreditUse) || draft.Date<original.Date
            || db.Scalar("SELECT Id FROM Movements WHERE ReversesId=$id;",("$id",draft.OriginalId)) is not null)
            throw new ArgumentException("Choose an unreversed supplier payment supporting the credit, and a use date on or after it.");
        var target=new RecordStore<Purchase>(store,"Purchase").Get(db,owner,draft.PurchaseId);
        if(target.Status!="Approved" || target.SupplierId!=original.SupplierId)throw new ArgumentException("Use this credit on an approved bill from the same supplier.");
        if(!new[]{"Original funding","Owner withdrawal"}.Contains(draft.SupplierCreditTreatment))throw new ArgumentException("Review how the owner-funded value should be treated.");
        bool boundOwner=original.PurchaseFunding!="Business cash" || PurchasePayments.Rows(db,original.PurchaseId).Any(r=>r.MovementId==draft.OriginalId && r.Sign==1 && r.DrawCents>0);
        if(boundOwner && draft.EconomicOwnerId.Length>0 && draft.EconomicOwnerId!=original.EconomicOwnerId)
            throw new ArgumentException("Retain the owner of the original private/funding share for this credit use.");
        return draft with{SupplierId=original.SupplierId,PurchaseFunding=original.PurchaseFunding,
            EconomicOwnerId=boundOwner?original.EconomicOwnerId:draft.EconomicOwnerId};
    }

    internal static List<PurchaseSettlement> Rows(DbWork db,BusinessStore store,OwnerSession owner,FinancialDraft draft)
    {
        var original=CorrectionRules.Original(db,owner,draft.OriginalId);
        var bill=new RecordStore<Purchase>(store,"Purchase").Get(db,owner,original.PurchaseId);
        if(draft.SourcePurchaseGeneration!=ValueState.Generation(db,owner,"Purchase",bill.Id))throw new ArgumentException("The source supplier credit changed. Save and review this use again.");
        var balances=PurchaseBalances.Lines(db,store,owner,bill,draft.Date!.Value).Where(l=>l.Gross<0).ToDictionary(l=>l.LineId);
        var all=PurchasePayments.Rows(db,bill.Id);
        var candidates=all.Where(r=>r.MovementId==draft.OriginalId && r.Sign==1 && r.ReversesId is null && balances.ContainsKey(r.LineId))
            .Select(r=>Candidate(r,all,balances[r.LineId],draft.Date.Value)).Where(c=>c.Capacity>0).ToList();
        if(draft.AmountCents>candidates.Sum(c=>c.Capacity))throw new ArgumentException("The credit use exceeds funded supplier credit or this original payment's compatible unused shares.");
        long[] shares=PaymentPlan.Allocate(draft.AmountCents,candidates.Select(c=>c.Capacity).ToArray());
        return candidates.Select((c,i)=>Take(c,shares[i],draft.Date.Value)).Where(r=>r.Cents>0).ToList();
    }

    private static CreditSource Candidate(PurchaseSettlement original,List<PurchaseSettlement> all,PurchaseLineBalance balance,DateOnly date)
    {
        var returned=all.Where(r=>(r.RefundsId==original.Id || r.ConsumesId==original.Id) && r.Date<=date).ToList();
        var remaining=new SupplierFundingShare(original.Cents+returned.Sum(r=>r.Sign*r.Cents),original.BusinessCents+returned.Sum(r=>r.Sign*r.BusinessCents),
            original.DrawCents+returned.Sum(r=>r.Sign*r.DrawCents));
        _=remaining.Portion(0);
        return new(original,remaining,-balance.Gross,-balance.Business);
    }

    private static PurchaseSettlement Take(CreditSource source,long cents,DateOnly date)
    {
        if(cents==0)return source.Original with{Cents=0};
        long lower=Math.Max(0,Math.Max(cents-(source.Remaining.Gross-source.Remaining.Business),cents-(source.Gross-source.Business)));
        long upper=Math.Min(cents,Math.Min(source.Remaining.Business,source.Business));
        long business=Math.Clamp(Money.Proportion(source.Remaining.Business,cents,source.Remaining.Gross),lower,upper);
        var share=source.Remaining.Portion(cents,business);
        return new(Guid.NewGuid().ToString(),"",source.Original.PurchaseId,source.Original.LineId,date,cents,business,null,-1,null,share.Draw,source.Original.Id);
    }

    private sealed record CreditSource(PurchaseSettlement Original,SupplierFundingShare Remaining,long Gross,long Business)
    {
        internal long Capacity=>checked(Math.Min(Remaining.Business,Business)+Math.Min(Remaining.Gross-Remaining.Business,Gross-Business));
    }
}
