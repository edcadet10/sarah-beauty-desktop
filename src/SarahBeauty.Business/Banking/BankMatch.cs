namespace SarahBeauty.Business.Banking;

public sealed record MatchPortion(string BankLineId,string JournalLineId,long Cents,string ReversesId="");
public sealed record BankMatchDraft(DateOnly Date,string Reason,MatchPortion[] Portions);
public sealed record BankMatchPreview(BankMatchDraft Draft,Dictionary<string,long> Generations);
public sealed record BankMatch(string Id,string GroupId,string BankLineId,string JournalLineId,long Cents,DateOnly Date,string ReversesId);
public sealed record BankMatchGroup(string Id,string ApprovedBy,string AtUtc,BankMatchDraft Draft);
public sealed record CashMatchLine(string Id,string MovementId,string AccountId,DateOnly Date,long SignedCents,string Description,DateOnly? ReversedDate,string ReversesMovementId="");
public sealed record BankMatchingSources(List<BankLine> BankLines,List<CashMatchLine> CashLines,List<MatchPosition> BankPositions,
    List<MatchPosition> CashPositions,List<BankMatch> History,List<BankMatchGroup> Approvals);
public sealed record MatchPosition(string Id,long SignedCents,long MatchedCents)
{
    public long RemainingCents=>checked(Math.Abs(SignedCents)-MatchedCents);
}
