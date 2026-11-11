using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class PostingValidation
{
    internal static void Validate(DbWork db, OwnerSession owner, BusinessStore store, FinancialDraft draft, List<Account> accounts)
    {
        if (!Enum.IsDefined(draft.Kind) || draft.Date is null || string.IsNullOrWhiteSpace(draft.Name))
            throw new ArgumentException("Choose a movement, date, and description.");
        if(draft.PaymentMethod.Length>100)throw new ArgumentException("Use a payment method of up to 100 characters.");
        if(draft.PurchaseId.Length>0 && draft.Kind is not (FinancialKind.PurchasePayment or FinancialKind.PurchaseRefund or FinancialKind.Reversal))
            throw new ArgumentException("Use the supplier payment workflow to settle a supplier bill.");
        if (draft.Kind != FinancialKind.Reversal && draft.Kind != FinancialKind.AssetDisposal) Money.Positive(draft.AmountCents);
        if (draft.AmountCents < 0 || draft.TaxCents < 0 || draft.TaxCents > draft.AmountCents
            || draft.FeeCents < 0 || draft.PrincipalCents < 0 || draft.InterestCents < 0)
            throw new ArgumentException("Review the movement's amounts.");
        if (draft.Kind is not (FinancialKind.Receipt or FinancialKind.Refund or FinancialKind.AssetDisposal) && draft.TaxCents != 0)
            throw new ArgumentException("Sales tax components belong to receipts and their refunds.");
        if (draft.Kind is not (FinancialKind.Receipt or FinancialKind.LoanPayment) && draft.FeeCents != 0)
            throw new ArgumentException("Record this fee as a separately classified expense.");
        CheckClosedPeriod(db, owner, draft.Date.Value);
        ValidateAccounts(draft, accounts);
        ValidateReferences(db, owner, store, draft);
        ValidateAmounts(draft);
    }

    internal static void CheckClosedPeriod(DbWork db, OwnerSession owner, DateOnly date)
    {
        if (db.Scalar("SELECT ClosedThrough FROM BookSettings WHERE BusinessId=$id;", ("$id", owner.BusinessId)) is string closed
            && date <= DateOnly.Parse(closed)) throw new ArgumentException("This date is in a closed period. Reopen it with a reason before changing the books.");
    }

    private static void ValidateAmounts(FinancialDraft draft)
    {
        if (draft.Kind is FinancialKind.Receipt or FinancialKind.Refund or FinancialKind.AssetDisposal && !draft.TaxResolved)
            throw new ArgumentException("Resolve the tax treatment before approving this receipt or refund.");
        if (draft.Kind == FinancialKind.Receipt && (draft.FeeCents > draft.AmountCents || draft.CustomerId.Length == 0))
            throw new ArgumentException("Choose the payer and a fee no greater than the gross receipt.");
        if (draft.Kind == FinancialKind.LoanPayment && checked(draft.PrincipalCents + draft.InterestCents + draft.FeeCents) != draft.AmountCents)
            throw new ArgumentException("Principal, interest, and fees must equal the cash payment.");
        if (draft.Kind != FinancialKind.LoanPayment && (draft.PrincipalCents != 0 || draft.InterestCents != 0))
            throw new ArgumentException("Principal and interest components belong to a loan payment.");
    }

    private static void ValidateAccounts(FinancialDraft draft, List<Account> accounts)
    {
        Account Required(string id) => accounts.SingleOrDefault(a => a.Id == id && a.Active)
            ?? throw new ArgumentException("Choose an active account from this business.");
        if (draft.Kind is not (FinancialKind.OwnerPaidExpense or FinancialKind.Depreciation or FinancialKind.AssetContribution or FinancialKind.Opening or FinancialKind.Reversal)
            && !(draft.Kind==FinancialKind.AssetDisposal && draft.AmountCents==0)
            && !(draft.Kind==FinancialKind.PurchasePayment && draft.PurchaseFunding!="Business cash")
            && !(draft.Kind==FinancialKind.PurchaseRefund && draft.SupplierRefundDestination!="Business cash"))
        {
            var cash = Required(draft.CashAccountId);
            if (cash.Type != "Asset" || cash.CashKind == "None") throw new ArgumentException("Choose the actual cash, bank, or processor account.");
        }
        if (draft.Kind == FinancialKind.Transfer && (draft.ToAccountId == draft.CashAccountId || Required(draft.ToAccountId).CashKind == "None"))
            throw new ArgumentException("Choose two different cash accounts for a transfer.");
        string? type = draft.Kind switch
        {
            FinancialKind.Expense or FinancialKind.OwnerPaidExpense => "Expense",
            FinancialKind.AssetPurchase or FinancialKind.AssetContribution => "Asset",
            _ => null
        };
        if (type is not null && (Required(draft.CounterAccountId).Type != type || Required(draft.CounterAccountId).CashKind != "None"))
            throw new ArgumentException($"Choose a noncash {type.ToLowerInvariant()} classification.");
        if (draft.Kind == FinancialKind.Receipt && Required(draft.CounterAccountId).Type != "Income"
            && Required(draft.CounterAccountId).Code != "2200")
            throw new ArgumentException("Classify this receipt as income or a refundable customer deposit.");
        if (draft.Kind is FinancialKind.SupplierRefund or FinancialKind.Refund or FinancialKind.Opening) Required(draft.CounterAccountId);
    }

    private static void ValidateReferences(DbWork db, OwnerSession owner, BusinessStore store, FinancialDraft draft)
    {
        BookingValidation.Customer(db, owner, draft.CustomerId);
        if (draft.BookingId.Length > 0) _ = new RecordStore<Booking>(store, "Booking").Get(db, owner, draft.BookingId);
        if (draft.SupplierId.Length > 0) _ = new RecordStore<Supplier>(store, "Supplier").Get(db, owner, draft.SupplierId);
        if (draft.Kind is FinancialKind.Contribution or FinancialKind.Withdrawal or FinancialKind.OwnerPaidExpense or FinancialKind.OwnerReimbursement or FinancialKind.AssetContribution)
            if (db.Scalar("SELECT Id FROM Owners WHERE Id=$id AND BusinessId=$business AND Active=1;",
                ("$id", draft.EconomicOwnerId), ("$business", owner.BusinessId)) is null) throw new ArgumentException("Choose the owner involved in this movement.");
        if (draft.Kind is FinancialKind.LoanProceeds or FinancialKind.LoanPayment)
        {
            var loan = new RecordStore<Loan>(store, "Loan").Get(db, owner, draft.LoanId);
            if(loan.Archived)throw new ArgumentException("Choose an active loan.");
            if (!loan.OpeningPrincipalCents.HasValue) throw new ArgumentException("Confirm this loan's opening principal before posting.");
            if(loan.OpeningPrincipalCents>0 && db.Scalar("""
                SELECT Id FROM OpeningBalances WHERE BusinessId=$business AND DimensionKind='Loan' AND DimensionId=$loan
                AND RevokedAt IS NULL AND Date<=$date;
                """,("$business",owner.BusinessId),("$loan",loan.Id),("$date",draft.Date!.Value.ToString("yyyy-MM-dd"))) is null)
                throw new ArgumentException("Approve this loan's evidenced ledger opening before posting proceeds or repayments.");
            if (loan.OpeningDate.HasValue && draft.Date < loan.OpeningDate) throw new ArgumentException("This movement precedes the loan opening date.");
        }
        if (draft.Kind is FinancialKind.AssetPurchase or FinancialKind.AssetContribution or FinancialKind.Depreciation or FinancialKind.AssetDisposal)
        {
            var asset = new RecordStore<Asset>(store, "Asset").Get(db, owner, draft.AssetId);
            if (asset.Archived || asset.DisposedDate.HasValue || asset.BusinessUseFraction is null || AssetDisposals.IsDisposed(db,owner,asset.Id,draft.Date!.Value))
                throw new ArgumentException("Confirm business use and use an asset that has not been disposed of.");
        }
    }
}
