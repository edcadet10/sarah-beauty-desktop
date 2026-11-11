using SarahBeauty.Business.Banking;

namespace SarahBeauty.Business.Finance;

public sealed record BookPeriodDraft(DateOnly From,DateOnly Through,string Reason,string EvidenceId);
public sealed record BookPeriodPreview(BookPeriodDraft Draft,DateOnly? PreviouslyClosedThrough,ReportSnapshot Reports,string[] BlockingIssues,ReconciliationSummary[] Reconciliations)
{
    public int Version {get;init;}=1;
    public bool ReadyToClose=>BlockingIssues.Length==0;
}
public sealed record ClosedBookPeriod(string Id,string ApprovedBy,string AtUtc,BookPeriodPreview Snapshot,string SnapshotHash,bool Reopened);
public sealed record BookPeriodSummary(string Id,DateOnly From,DateOnly Through,string ApprovedBy,string AtUtc,bool Reopened);
public sealed record BookPeriodReopen(string Id,string PeriodId,string Reason,string OwnerId,string AtUtc);
