using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class CreditRefundRules
{
    internal static void Validate(DbWork db,OwnerSession owner,FinancialDraft draft)
    {
        if(draft.CreditId.Length==0)return;
        if(draft.Kind is not (FinancialKind.Refund or FinancialKind.Reversal))throw new ArgumentException("A funded credit link belongs to its cash refund or reversal.");
        var credit=CreditValues.Get(db,owner,draft.CreditId);
        if(draft.Kind==FinancialKind.Reversal)
        {
            _=Release(db,draft.OriginalId);
            return;
        }
        if(credit.PaymentId!=draft.OriginalId)throw new ArgumentException("The credit refund must use its original receipt.");
        var available=CreditValues.Available(db,owner,credit.Id,draft.Date!.Value);
        if(draft.AmountCents>available.Cents || draft.TaxCents>available.TaxCents)
            throw new ArgumentException("This refund exceeds the unused funded credit or its tax component.");
        ValueAvailability.Validate(new(available.Cents-draft.AmountCents,available.TaxCents-draft.TaxCents),"Remaining customer credit",draft.Date.Value);
    }

    internal static bool AfterPosting(DbWork db,OwnerSession owner,FinancialDraft draft,string movementId)
    {
        if(draft.CreditId.Length==0)return false;
        var credit=CreditValues.Get(db,owner,draft.CreditId);
        if(draft.Kind==FinancialKind.Refund)
            CreditValues.Fund(db,owner,credit.Id,credit.PaymentId,"Release",draft.Date!.Value,draft.AmountCents,draft.TaxCents,
                draft.Name,draft.Id,movementId);
        else if(draft.Kind==FinancialKind.Reversal)
        {
            var release=Release(db,draft.OriginalId);
            if(release.CreditId!=credit.Id || release.PaymentId!=credit.PaymentId)throw new ArgumentException("The original credit refund funding does not match.");
            CreditValues.Fund(db,owner,credit.Id,credit.PaymentId,"Reserve",draft.Date!.Value,release.Cents,release.Tax,
                draft.Name,draft.Id,movementId,release.Id);
        }
        else throw new ArgumentException("Unsupported credit funding correction.");
        CreditValues.ValidateHistory(db,owner,credit.Id);
        ValueState.Advance(db,owner,"Credit",credit.Id);
        return true;
    }

    private static ReleasedFunding Release(DbWork db,string refundId)=>db.Read("""
        SELECT Id,CreditId,PaymentId,Cents,TaxCents FROM CreditFunding WHERE RefundMovementId=$id AND Kind='Release';
        """,r=>new ReleasedFunding(r.GetString(0),r.GetString(1),r.GetString(2),r.GetInt64(3),r.GetInt64(4)),("$id",refundId)).SingleOrDefault()
        ??throw new ArgumentException("The original credit refund has no matching funding release.");

    private sealed record ReleasedFunding(string Id,string CreditId,string PaymentId,long Cents,long Tax);
}
