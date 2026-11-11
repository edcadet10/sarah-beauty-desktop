using SarahBeauty.Business.Finance;

namespace SarahBeauty.Business.Resources;

public sealed record TripPaymentDraft(string TripId,string PaymentId,DateOnly Date,long Cents,string PayeeReview,string EvidenceId);
public sealed record TripPaymentPreview(TripPaymentDraft Draft,MileageTrip Trip,PostedMovement Payment,string ClaimBasisHash,long Watermark,long ClaimPaidCents,long PaymentAvailableCents);
public sealed record LinkedTripPayment(string Id,string ApprovedBy,string AtUtc,TripPaymentPreview Snapshot,string SnapshotHash);
public sealed record TripPaymentReleaseDraft(string LinkId,DateOnly Date,long Cents,string Reason,string EvidenceId);
public sealed record TripPaymentReleasePreview(TripPaymentReleaseDraft Draft,LinkedTripPayment Original,long Watermark,long RemainingCents);
public sealed record ReleasedTripPayment(string Id,string ApprovedBy,string AtUtc,TripPaymentReleasePreview Snapshot,string SnapshotHash);
