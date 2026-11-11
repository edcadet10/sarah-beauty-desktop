namespace SarahBeauty.Business.Banking;

public sealed record StatementError(string Id,string BankLineId,long Cents,string Explanation,string EvidenceId);
public sealed record StatementErrorResolution(string ErrorId,string BankLineId,long Cents,string Explanation,string EvidenceId);
public sealed record CarriedStatementError(string Id,string BankLineId,DateOnly Date,long SignedCents,long RemainingCents,string Explanation,string EvidenceId);

internal sealed record StatementErrorCalculation(long AdjustmentCents,Dictionary<string,long> ClaimedBankCents,CarriedStatementError[] RemainingErrors);
