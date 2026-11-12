namespace SarahBeauty.Business.Ownership;

internal static class OwnerInterestRules
{
    internal static OwnerInterestDraft Normalize(OwnerInterestDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if(draft.ExpectedRevision<0 || draft.ExpectedRevision==long.MaxValue || draft.EffectiveFrom==default)
            throw new ArgumentException("Choose an effective date and reload the current ownership revision.");
        string reason=draft.Reason?.Trim()??"";
        if(reason.Length is <1 or >1000)throw new ArgumentException("Enter a confirmation or correction reason of at most 1,000 characters.");
        var shares=NormalizeShares(draft.Shares);
        return draft with{Reason=reason,Shares=shares,SupersedesId=OptionalId(draft.SupersedesId),EvidenceVersionId=OptionalId(draft.EvidenceVersionId)};
    }

    internal static IReadOnlyList<OwnerInterestInput> NormalizeShares(IReadOnlyList<OwnerInterestInput>? values)
    {
        if(values is null || values.Count==0)throw new ArgumentException("Enter each owner's confirmed percentage. Unknown percentages cannot be saved.");
        var shares=values.Select(value=>
        {
            if(value is null || value.Fraction is not decimal fraction || fraction<0 || fraction>1 || decimal.Round(fraction,6)!=fraction)
                throw new ArgumentException("Enter a percentage from 0 to 100 with at most four decimal places for every owner.");
            return value with{OwnerId=RequiredId(value.OwnerId)};
        }).OrderBy(value=>value.OwnerId,StringComparer.Ordinal).ToArray();
        if(shares.Select(value=>value.OwnerId).Distinct(StringComparer.Ordinal).Count()!=shares.Length || shares.Sum(value=>value.Fraction!.Value)!=1m)
            throw new ArgumentException("List each owner once and make the confirmed percentages total exactly 100%.");
        return Array.AsReadOnly(shares);
    }

    private static string RequiredId(string? value)=>Guid.TryParseExact(value,"D",out var id)?id.ToString():throw new ArgumentException("Choose an existing owner or ownership/evidence version.");
    private static string? OptionalId(string? value)=>string.IsNullOrWhiteSpace(value)?null:RequiredId(value);
}
