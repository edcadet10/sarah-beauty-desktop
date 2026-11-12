using System.Numerics;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class BalanceGuards
{
    internal static void Validate(DbWork db, OwnerSession owner, FinancialDraft draft)
    {
        if (draft.LoanId.Length > 0) Nonnegative(db, owner, "A.Code='2300'", "M.LoanId=$dimension", draft.LoanId, creditNormal: true, "Loan principal");
        if (draft.EconomicOwnerId.Length > 0 && draft.Kind is FinancialKind.OwnerPaidExpense or FinancialKind.OwnerReimbursement or FinancialKind.PurchaseRefund or FinancialKind.SupplierCreditUse or FinancialKind.Reversal)
            Nonnegative(db, owner, "A.Code='2100'", "M.OwnerId=$dimension", draft.EconomicOwnerId, true, "Owner reimbursement payable");
        if (draft.Kind is FinancialKind.TaxRemittance or FinancialKind.Refund or FinancialKind.TaxRecovery or FinancialKind.Reversal)
            Nonnegative(db, owner, "A.Code='2000'", "1=1", "", true, "Tax liability");
    }

    internal static void ValidateAsset(DbWork db,OwnerSession owner,string asset)=>
        Nonnegative(db,owner,"A.Type='Asset' AND A.CashKind='None'","J.AssetId=$dimension",asset,false,"Asset carrying value");

    private static void Nonnegative(DbWork db, OwnerSession owner, string accountFilter, string dimensionFilter,
        string dimension, bool creditNormal, string label)
    {
        // Filters are fixed internal SQL fragments; all record values remain parameters.
        var amounts = db.Read($"""
            SELECT M.Date,J.DebitCents,J.CreditCents FROM JournalLines J
            JOIN Movements M ON M.Id=J.MovementId JOIN Accounts A ON A.Id=J.AccountId
            WHERE M.BusinessId=$business AND {accountFilter} AND {dimensionFilter}
            ORDER BY M.Date;
            """, r => (Date: r.GetString(0), Debit: r.GetInt64(1), Credit: r.GetInt64(2)), ("$business", owner.BusinessId), ("$dimension", dimension));
        BigInteger balance = 0;
        foreach (var day in amounts.GroupBy(v=>v.Date,StringComparer.Ordinal))
        {
            BigInteger amount=day.Aggregate(BigInteger.Zero,(sum,line)=>sum+(BigInteger)line.Debit-line.Credit);
            balance += creditNormal ? -amount : amount;
            if (balance < 0) throw new ArgumentException($"{label} would become negative on {day.Key}.");
        }
    }
}
