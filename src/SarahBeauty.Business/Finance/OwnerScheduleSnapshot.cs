namespace SarahBeauty.Business.Finance;

public sealed record OwnerSchedulePosition(string OwnerId,string Name,bool Active,long ContributionsCents,long DrawsCents,
    long PayableAddedCents,long PayableClearedCents,long ReimbursementsCents,long LoanProceedsCents,long LoanPrincipalPaidCents,long AttributedLoanPrincipalCents);

public sealed record OwnerAccountPosition(string OwnerId,string AccountCode,long OpeningDebitLessCreditCents,long PeriodDebitCents,
    long PeriodCreditCents,long ClosingDebitLessCreditCents);

public sealed record OwnerScheduleEntry(string OwnerId,string MovementId,DateOnly Date,string Kind,string EffectiveKind,string Memo,
    string AccountCode,string AccountName,long DebitCents,long CreditCents,string Attribution,string LoanId="");

public sealed record OwnerLoanBalance(string LoanId,string LoanName,string OwnerId,string Attribution,long? PrincipalCents);

public sealed record OwnerScheduleSnapshot(int Version,List<OwnerSchedulePosition> Positions,List<OwnerAccountPosition> Accounts,
    List<OwnerScheduleEntry> Entries,List<OwnerLoanBalance> LoanBalances);
