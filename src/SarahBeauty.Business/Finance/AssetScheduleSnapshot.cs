namespace SarahBeauty.Business.Finance;

public sealed record AssetRegisterFacts(string AssetId,string Name,bool Archived,string LegalOwner,DateOnly? AcquisitionDate,
    DateOnly? InServiceDate,long? CostCents,long? TaxBasisCents,decimal? BusinessUseFraction,string Funding,string EvidenceId,int Revision);

public sealed record AssetSchedulePosition(AssetRegisterFacts CurrentFacts,long? OpeningCostCents,long? OpeningDepreciationCents,
    long CostIncreasesCents,long CostReductionsCents,long DepreciationAddedCents,long DepreciationClearedCents,
    long? ClosingCostCents,long? ClosingDepreciationCents,long? CarryingCents,bool DisposedAsOf);

public sealed record AssetScheduleEntry(string AssetId,string MovementId,DateOnly Date,string Kind,string Memo,
    string AccountCode,string AccountName,long DebitCents,long CreditCents,bool IsBookValue);

public sealed record AssetScheduleEvent(string AssetId,string MovementId,DateOnly Date,string Kind,string Memo);

public sealed record AssetScheduleSnapshot(int Version,List<AssetSchedulePosition> Positions,List<AssetScheduleEntry> Entries,List<AssetScheduleEvent> Events);
