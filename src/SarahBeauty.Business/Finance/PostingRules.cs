using SarahBeauty.Business.Rules;

namespace SarahBeauty.Business.Finance;

internal static class PostingRules
{
    internal static List<JournalLine> Build(FinancialDraft draft, IReadOnlyList<Account> accounts)
    {
        string Code(string code) => accounts.Single(a => a.Code == code && a.Active).Id;
        var lines = draft.Kind switch
        {
            FinancialKind.Receipt => Receipt(draft, Code("5020"), Code("2000")),
            FinancialKind.LoanPayment => LoanPayment(draft, Code("2300"), Code("5030"), Code("5035")),
            FinancialKind.Opening => draft.OpeningCredit
                ? Pair(Code("3200"), draft.CounterAccountId, draft.AmountCents)
                : Pair(draft.CounterAccountId, Code("3200"), draft.AmountCents),
            _ => Simple(draft, Code)
        };
        lines = lines.Where(l => l.DebitCents != 0 || l.CreditCents != 0).ToList();
        ValidateBalance(lines);
        return lines;
    }

    private static List<JournalLine> Simple(FinancialDraft d, Func<string,string> code)
    {
        var (debit,credit) = d.Kind switch
        {
            FinancialKind.UnclassifiedReceipt => (d.CashAccountId,code("2400")),
            FinancialKind.Expense or FinancialKind.AssetPurchase => (d.CounterAccountId,d.CashAccountId),
            FinancialKind.Transfer => (d.ToAccountId,d.CashAccountId),
            FinancialKind.Contribution => (d.CashAccountId,code("3000")),
            FinancialKind.Withdrawal => (code("3100"),d.CashAccountId),
            FinancialKind.LoanProceeds => (d.CashAccountId,code("2300")),
            FinancialKind.OwnerPaidExpense => (d.CounterAccountId,code("2100")),
            FinancialKind.OwnerReimbursement => (code("2100"),d.CashAccountId),
            FinancialKind.AssetContribution => (d.CounterAccountId,code("3000")),
            FinancialKind.Depreciation => (code("5040"),code("1590")),
            FinancialKind.TaxRemittance => (code("2000"),d.CashAccountId),
            FinancialKind.SupplierRefund => (d.CashAccountId,d.CounterAccountId),
            FinancialKind.Refund => (d.CounterAccountId,d.CashAccountId),
            _ => throw new ArgumentException("Use the dedicated correction or disposal command.")
        };
        if(d.Kind==FinancialKind.Refund)
            return [new(debit,d.AmountCents-d.TaxCents,0),new(code("2000"),d.TaxCents,0),new(credit,0,d.AmountCents)];
        return Pair(debit,credit,d.AmountCents);
    }

    private static List<JournalLine> Pair(string debit,string credit,long cents) =>
        [new(debit,cents,0),new(credit,0,cents)];

    private static List<JournalLine> Receipt(FinancialDraft d,string fee,string tax) =>
        [new(d.CashAccountId,d.AmountCents-d.FeeCents,0),new(fee,d.FeeCents,0),
         new(d.CounterAccountId,0,d.AmountCents-d.TaxCents),new(tax,0,d.TaxCents)];

    private static List<JournalLine> LoanPayment(FinancialDraft d,string principal,string interest,string fee) =>
        [new(principal,d.PrincipalCents,0),new(interest,d.InterestCents,0),new(fee,d.FeeCents,0),new(d.CashAccountId,0,d.AmountCents)];

    internal static void ValidateBalance(IReadOnlyList<JournalLine> lines)
    {
        if (lines.Count < 2 || lines.Any(l => l.DebitCents < 0 || l.CreditCents < 0
            || (l.DebitCents > 0) == (l.CreditCents > 0))
            || lines.Sum(l => l.DebitCents) != lines.Sum(l => l.CreditCents))
            throw new ArgumentException("The proposed journal does not balance.");
    }
}
