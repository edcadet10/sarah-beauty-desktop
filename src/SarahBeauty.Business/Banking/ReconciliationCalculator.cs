namespace SarahBeauty.Business.Banking;

internal static class ReconciliationCalculator
{
    internal static ReconciliationPreview Calculate(ReconciliationSources sources,ReconciliationDraft draft)
    {
        Validate(draft,sources);
        var cash=sources.Cash.ToDictionary(c=>c.Id);
        long deposits=ReconciliationSourcesReader.Cents(draft.Outstanding.Where(o=>cash[o.JournalLineId].SignedCents>0).Select(o=>o.Cents));
        long payments=ReconciliationSourcesReader.Cents(draft.Outstanding.Where(o=>cash[o.JournalLineId].SignedCents<0).Select(o=>o.Cents));
        long corrections=ReconciliationSourcesReader.Cents(sources.CorrectionChoices.Where(c=>draft.PostedCorrectionIds.Contains(c.MovementId)).Select(c=>c.SignedCents));
        long adjusted=ReconciliationSourcesReader.Cents([sources.Statement.ClosingCents,deposits,-payments]);
        long before=ReconciliationSourcesReader.Difference(sources.LedgerClosingCents,corrections);
        long difference=ReconciliationSourcesReader.Difference(adjusted,sources.LedgerClosingCents);
        var supported=draft.Outstanding.Select(o=>o.JournalLineId).ToHashSet();
        return new(draft,sources,deposits,payments,corrections,before,adjusted,difference,sources.Cash.Where(c=>c.RemainingCents>0 && !supported.Contains(c.Id)).Select(c=>c.Id).ToArray());
    }

    private static void Validate(ReconciliationDraft draft,ReconciliationSources sources)
    {
        if(string.IsNullOrWhiteSpace(draft.Reason))throw new ArgumentException("Record the reason for this reconciliation review.");
        var cash=sources.Cash.ToDictionary(c=>c.Id);
        if(draft.Outstanding.Select(o=>o.JournalLineId).Distinct().Count()!=draft.Outstanding.Length)throw new ArgumentException("Review each outstanding cash entry once.");
        foreach(var item in draft.Outstanding)
            if(!cash.TryGetValue(item.JournalLineId,out var line) || item.Cents<=0 || item.Cents!=line.RemainingCents
                || string.IsNullOrWhiteSpace(item.EvidenceId) || string.IsNullOrWhiteSpace(item.Explanation))
                throw new ArgumentException("Support the full remaining amount of each outstanding deposit or payment with its evidence and explanation.");
        if(draft.PostedCorrectionIds.Distinct().Count()!=draft.PostedCorrectionIds.Length || draft.PostedCorrectionIds.Any(id=>!sources.CorrectionChoices.Any(c=>c.MovementId==id)))
            throw new ArgumentException("Link distinct, evidenced corrections already posted to this cash account by period end.");
    }
}
