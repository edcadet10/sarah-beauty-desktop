namespace SarahBeauty.Business.Attachments;

// These values are retained in link snapshots. Append new kinds without reordering existing values.
public enum EvidenceTargetKind {Customer,Booking,MoneyDraft,PostedMoney,IssuedDocument,Supplier,Purchase,Asset,Loan,Supply,Trip,TaxPeriod,Artist,Service,QuoteDraft,InvoiceDraft,CreditNoteDraft,ReceiptDraft,PaymentDraft,CreditDraft,CancellationDraft}
public sealed record EvidenceTarget(EvidenceTargetKind Kind,string Id,string Name,int Revision,string Status,bool Archived);
public sealed record EvidenceLinkDraft(string AttachmentVersionId,EvidenceTargetKind TargetKind,string TargetId,int ExpectedTargetRevision,string Purpose);
public sealed record EvidenceLink(string Id,string BusinessId,string AttachmentVersionId,string AttachmentSnapshotHash,EvidenceTarget Target,string Purpose,string OwnerId,string AtUtc);
public sealed record EvidenceLinkRelease(string Id,string BusinessId,string LinkId,string LinkHash,string Reason,string OwnerId,string AtUtc);
public sealed record LinkedEvidence(EvidenceLink Link,string SnapshotHash,EvidenceLinkRelease? Release,string? ReleaseHash);
