namespace SarahBeauty.Business.Finance;

public sealed record TaxFilingReview(TaxFilingDecision Decision,string OwnerId,string OwnerNameAtCapture,string AtUtc);
public sealed record TaxFilingPeriodSnapshot(string Id,string Name,string Jurisdiction,DateOnly? From,DateOnly? Through,DateOnly? DueDate,
    string StatusAtCapture,bool Archived,int Revision,TaxFilingReview[] History);
public sealed record TaxFilingSnapshot(int Version,List<TaxFilingPeriodSnapshot> Periods);
