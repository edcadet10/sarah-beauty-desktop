namespace SarahBeauty.Business.Ownership;

public sealed record OwnerInterestInput(string OwnerId,decimal? Fraction);
public sealed record OwnerInterestDraft(long ExpectedRevision,DateOnly EffectiveFrom,IReadOnlyList<OwnerInterestInput> Shares,
    string Reason,string? SupersedesId=null,string? EvidenceVersionId=null);
public sealed record ConfirmedOwnerShare(string OwnerId,string OwnerName,decimal Fraction);
public sealed record OwnerInterestVersion(int FormatVersion,string Id,string BusinessId,long Revision,DateOnly EffectiveFrom,
    string? SupersedesId,IReadOnlyList<ConfirmedOwnerShare> Shares,string Reason,string? EvidenceVersionId,
    string? EvidenceSnapshotHash,string RecordedByOwnerId,string RecordedAtUtc);
