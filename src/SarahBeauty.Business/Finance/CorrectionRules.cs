using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class CorrectionRules
{
    internal static FinancialDraft Prepare(DbWork db, OwnerSession owner, FinancialDraft draft)
    {
        if (draft.Kind is not (FinancialKind.Reversal or FinancialKind.Refund or FinancialKind.SupplierRefund)) return draft;
        var original = Original(db, owner, draft.OriginalId);
        if (draft.Date < original.Date) throw new ArgumentException("A correction cannot precede its original movement.");
        if (db.Scalar("SELECT Id FROM Movements WHERE ReversesId=$original;", ("$original", draft.OriginalId)) is not null)
            throw new ArgumentException("The original movement has already been reversed.");
        if (draft.Kind == FinancialKind.Reversal) return Reversal(db, owner, draft, original);
        var required = draft.Kind == FinancialKind.Refund ? FinancialKind.Receipt : original.Kind;
        if ((draft.Kind == FinancialKind.SupplierRefund && original.Kind is not (FinancialKind.Expense or FinancialKind.AssetPurchase))
            || (draft.Kind == FinancialKind.Refund && original.Kind != required)
            || draft.CounterAccountId != original.CounterAccountId || draft.CustomerId != original.CustomerId)
            throw new ArgumentException("The refund must keep the original source, customer, and classification.");
        CheckRefundLimit(db, owner, draft, original);
        return draft with { LoanId = original.LoanId, AssetId = original.AssetId, SupplierId = original.SupplierId, BookingId = original.BookingId };
    }

    internal static FinancialDraft Original(DbWork db, OwnerSession owner, string id) =>
        db.Scalar("SELECT DataJson FROM Movements WHERE Id=$id AND BusinessId=$business;",
            ("$id", id), ("$business", owner.BusinessId)) is string json ? JsonSerializer.Deserialize<FinancialDraft>(json)!
            : throw new ArgumentException("Choose an original posted movement from this business.");

    private static FinancialDraft Reversal(DbWork db, OwnerSession owner, FinancialDraft draft, FinancialDraft original)
    {
        if (original.Kind == FinancialKind.Reversal || db.Scalar("""
            SELECT M.Id FROM Movements M WHERE M.OriginalId=$original
            AND NOT EXISTS(SELECT 1 FROM Movements R WHERE R.ReversesId=M.Id) LIMIT 1;
            """, ("$original", draft.OriginalId)) is not null)
            throw new ArgumentException("Resolve dependent refunds or corrections before reversing this movement.");
        return draft with { AmountCents = original.AmountCents, CustomerId = original.CustomerId,
            EconomicOwnerId = original.EconomicOwnerId, BookingId = original.BookingId, LoanId = original.LoanId,
            AssetId = original.AssetId, SupplierId = original.SupplierId, CreditId=original.CreditId,
            PurchaseId=original.PurchaseId,PurchaseFunding=original.PurchaseFunding,SupplierRefundDestination=original.SupplierRefundDestination };
    }

    private static void CheckRefundLimit(DbWork db, OwnerSession owner, FinancialDraft draft, FinancialDraft original)
    {
        var refunds = db.Read("""
            SELECT M.GrossCents,M.TaxCents FROM Movements M WHERE M.BusinessId=$business AND M.OriginalId=$original
            AND NOT EXISTS(SELECT 1 FROM Movements R WHERE R.ReversesId=M.Id);
            """, r => (Gross: r.GetInt64(0), Tax: r.GetInt64(1)), ("$business", owner.BusinessId), ("$original", draft.OriginalId));
        if (checked(refunds.Sum(r => r.Gross) + draft.AmountCents) > original.AmountCents
            || checked(refunds.Sum(r => r.Tax) + draft.TaxCents) > original.TaxCents)
            throw new ArgumentException("The refund exceeds the original amount or its remaining tax component.");
    }

    internal static List<JournalLine> Lines(DbWork db, OwnerSession owner, FinancialDraft draft, List<Account> accounts)
    {
        if (draft.Kind == FinancialKind.Reversal)
            return db.Read("SELECT AccountId,CreditCents,DebitCents,COALESCE(AssetId,'') FROM JournalLines WHERE MovementId=$id;",
                r => new JournalLine(r.GetString(0), r.GetInt64(1), r.GetInt64(2),r.GetString(3)), ("$id", draft.OriginalId));
        return AssetDisposals.Lines(db,owner,draft,accounts);
    }
}
