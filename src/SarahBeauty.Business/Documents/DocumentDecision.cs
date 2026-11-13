using SarahBeauty.Business.Attachments;

namespace SarahBeauty.Business.Documents;

public enum DocumentDecisionAction { DeclineQuote,WithdrawQuote,VoidQuote,VoidInvoice }
public sealed record DocumentDecisionDraft(string VersionId,string DocumentSnapshotHash,string ExpectedStatus,DocumentDecisionAction Action,
    DateOnly DecisionDate,string Reason,ManagedEvidenceFile? Evidence=null);
public sealed record DocumentDecision(int Version,string Id,string BusinessId,string DocumentId,string VersionId,string RequestId,
    DocumentDecisionAction Action,DateOnly DecisionDate,string PriorStatus,string ResultStatus,string DocumentSnapshotHash,string Reason,
    ManagedEvidenceFile? Evidence,string OwnerId,string RecordedAtUtc);
