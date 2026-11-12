namespace SarahBeauty.Business.Setup;

public sealed record BusinessProfileChange(long ExpectedRevision,BusinessProfile Profile,string Reason);
public sealed record BusinessProfileVersion(int Version,string Id,string BusinessId,long Revision,string RequestId,string Mode,
    BusinessProfile Profile,string ChangeReason,string RecordedByOwnerId,string RecordedAtUtc);
