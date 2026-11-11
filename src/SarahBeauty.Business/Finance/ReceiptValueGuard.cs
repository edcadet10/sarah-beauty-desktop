using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class ReceiptValueGuard
{
    internal static void AfterPosting(DbWork db,OwnerSession owner,FinancialDraft draft,string movementId)
    {
        string? payment=draft.Kind switch
        {
            FinancialKind.Receipt or FinancialKind.UnclassifiedReceipt or FinancialKind.ReceiptClassification=>movementId,
            FinancialKind.Refund=>draft.OriginalId,
            FinancialKind.Reversal=>RootForReversal(db,owner,draft.OriginalId),
            _=>null
        };
        if(payment is null)return;
        ValueAvailability.ValidatePaymentHistory(db,owner,payment);
        ValueState.Advance(db,owner,"Payment",payment);
    }

    private static string? RootForReversal(DbWork db,OwnerSession owner,string originalId)
    {
        var original=CorrectionRules.Original(db,owner,originalId);
        return original.Kind is FinancialKind.Receipt or FinancialKind.UnclassifiedReceipt or FinancialKind.ReceiptClassification?originalId:
            original.Kind==FinancialKind.Refund?original.OriginalId:null;
    }
}
