namespace SarahBeauty.Business.Finance;

internal static class TaxRefunds
{
    internal static void Validate(FinancialDraft draft)
    {
        if(draft.TaxRefundAllocation is null || draft.Kind==FinancialKind.Reversal)return;
        var split=draft.TaxRefundAllocation;
        if(draft.Kind!=FinancialKind.Refund || split.LiabilityCents<0 || split.RecoverableCents<0 || split.NonrecoverableCents<0
            || checked(split.LiabilityCents+split.RecoverableCents+split.NonrecoverableCents)!=draft.TaxCents)
            throw new ArgumentException("The refunded tax must equal its nonnegative liability, recoverable, and nonrecoverable parts.");
        if((split.RecoverableCents>0 || split.NonrecoverableCents>0) && string.IsNullOrWhiteSpace(draft.EvidenceId))
            throw new ArgumentException("Record the evidence for the refunded tax treatment. Recovery eligibility requires an explicit review.");
    }

    internal static List<JournalLine> Lines(FinancialDraft draft,List<Account> accounts)
    {
        var split=draft.TaxRefundAllocation??new(draft.TaxCents,0,0);
        string Code(string code)=>accounts.Single(a=>a.Code==code && a.Active).Id;
        return new List<JournalLine>{new(draft.CounterAccountId,draft.AmountCents-draft.TaxCents,0),
            new(Code("2000"),split.LiabilityCents,0),new(Code("1250"),split.RecoverableCents,0),new(Code("5050"),split.NonrecoverableCents,0),
            new(draft.CashAccountId,0,draft.AmountCents)}.Where(l=>l.DebitCents>0 || l.CreditCents>0).ToList();
    }
}
