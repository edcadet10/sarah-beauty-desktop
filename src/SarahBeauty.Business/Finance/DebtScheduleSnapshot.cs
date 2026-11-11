namespace SarahBeauty.Business.Finance;

public sealed record LoanRegisterFacts(string LoanId,string Name,bool Archived,DateOnly? OpeningDate,long? OpeningPrincipalCents,
    DateOnly? DueDate,string Terms,string OwnerLenderId,string OwnerLenderName,string EvidenceId,int Revision);

public sealed record DebtSchedulePosition(LoanRegisterFacts CurrentFacts,long? PostedOpeningPrincipalCents,long OpeningAdjustmentsCents,
    long ProceedsCents,long PrincipalPaidCents,long OtherPrincipalChangeCents,long InterestCents,long FeesCents,long? ClosingPrincipalCents);

public sealed record DebtScheduleEntry(string LoanId,string MovementId,DateOnly Date,string Kind,string EffectiveKind,string Memo,
    string AccountCode,string AccountName,long DebitCents,long CreditCents,LoanLenderSnapshot? CapturedLender);

public sealed record DebtScheduleSnapshot(int Version,List<DebtSchedulePosition> Positions,List<DebtScheduleEntry> Entries);
