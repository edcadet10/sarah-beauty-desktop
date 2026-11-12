namespace SarahBeauty.Business.Documents;

public sealed record ClientDraftDiscardRequest(DocumentKind Kind,string DraftId,int ExpectedRevision,string DraftSnapshotHash,string Reason);
public sealed record ClientDraftDiscard(int Version,string Id,string BusinessId,string DraftId,DocumentKind Kind,int DraftRevision,string RequestId,string Mode,
    string DraftSnapshotJson,string DraftSnapshotHash,string Reason,string OwnerId,string RecordedAtUtc);
