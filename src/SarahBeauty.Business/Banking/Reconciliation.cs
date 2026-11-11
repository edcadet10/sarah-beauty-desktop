namespace SarahBeauty.Business.Banking;

public sealed record OutstandingCash(string JournalLineId,long Cents,string Explanation,string EvidenceId);
public sealed record ReconciliationDraft(string StatementId,string Reason)
{
    public OutstandingCash[] Outstanding {get;init;}=[];
    public string[] PostedCorrectionIds {get;init;}=[];
    public StatementError[] StatementErrors {get;init;}=[];
    public StatementErrorResolution[] BankCorrections {get;init;}=[];
}
public sealed record ReconciliationLine(string Id,DateOnly Date,long SignedCents,long RemainingCents,string Description,string Kind="");
public sealed record PostedBookCorrection(string MovementId,DateOnly Date,long SignedCents,string Description,string EvidenceId);
public sealed record ReconciliationSources(StatementImport Statement,string BusinessName,string Mode,string AccountName,long Watermark,string GeneratedAtUtc,
    bool OpeningConfirmed,long LedgerClosingCents,ReconciliationLine[] Bank,ReconciliationLine[] Cash,PostedBookCorrection[] CorrectionChoices)
{
    public string[] PriorReconciliationIds {get;init;}=[];
    public string[] ContinuityWarnings {get;init;}=[];
    public CarriedStatementError[] CarriedErrors {get;init;}=[];
}
public sealed record CompletedReconciliation(string Id,string AccountId,string StatementId,DateOnly AsOfDate,string ApprovedBy,string AtUtc,
    ReconciliationPreview Snapshot,string SnapshotHash,bool Reopened);
public sealed record ReconciliationReopen(string Id,string ReconciliationId,string Reason,string OwnerId,string AtUtc);
public sealed record ReconciliationSummary(string Id,string AccountId,string StatementId,DateOnly AsOfDate,string ApprovedBy,string AtUtc,bool Reopened);
public sealed record ReconciliationPreview(ReconciliationDraft Draft,ReconciliationSources Sources,long OutstandingDepositsCents,long OutstandingPaymentsCents,
    long PostedBookCorrectionsCents,long LedgerBeforeCorrectionsCents,long AdjustedStatementCents,long DifferenceCents,string[] UnexplainedCashIds)
{
    public long AdjustedLedgerCents=>Sources.LedgerClosingCents;
    public Dictionary<string,long> BankErrorClaims {get;init;}=[];
    public long StatementErrorAdjustmentCents {get;init;}
    public CarriedStatementError[] RemainingStatementErrors {get;init;}=[];
    public int UnexplainedBankCount=>Sources.Bank.Count(b=>b.RemainingCents>BankErrorClaims.GetValueOrDefault(b.Id));
    public bool ReadyToComplete=>Sources.OpeningConfirmed && Sources.ContinuityWarnings.Length==0 && DifferenceCents==0 && UnexplainedBankCount==0 && UnexplainedCashIds.Length==0;
}
