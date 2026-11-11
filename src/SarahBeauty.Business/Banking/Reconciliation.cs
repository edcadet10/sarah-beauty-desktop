namespace SarahBeauty.Business.Banking;

public sealed record OutstandingCash(string JournalLineId,long Cents,string Explanation,string EvidenceId);
public sealed record ReconciliationDraft(string StatementId,string Reason)
{
    public OutstandingCash[] Outstanding {get;init;}=[];
    public string[] PostedCorrectionIds {get;init;}=[];
}
public sealed record ReconciliationLine(string Id,DateOnly Date,long SignedCents,long RemainingCents,string Description,string Kind="");
public sealed record PostedBookCorrection(string MovementId,DateOnly Date,long SignedCents,string Description,string EvidenceId);
public sealed record ReconciliationSources(StatementImport Statement,string BusinessName,string Mode,string AccountName,long Watermark,string GeneratedAtUtc,
    bool OpeningConfirmed,long LedgerClosingCents,ReconciliationLine[] Bank,ReconciliationLine[] Cash,PostedBookCorrection[] CorrectionChoices);
public sealed record ReconciliationPreview(ReconciliationDraft Draft,ReconciliationSources Sources,long OutstandingDepositsCents,long OutstandingPaymentsCents,
    long PostedBookCorrectionsCents,long LedgerBeforeCorrectionsCents,long AdjustedStatementCents,long DifferenceCents,string[] UnexplainedCashIds)
{
    public long AdjustedLedgerCents=>Sources.LedgerClosingCents;
    public int UnexplainedBankCount=>Sources.Bank.Count(b=>b.RemainingCents>0);
    public bool ReadyToComplete=>Sources.OpeningConfirmed && DifferenceCents==0 && UnexplainedBankCount==0 && UnexplainedCashIds.Length==0;
}
