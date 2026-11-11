using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

public sealed record TaxPeriod : BusinessRecord
{
    public string Jurisdiction {get;init;}="";
    public DateOnly? From {get;init;}
    public DateOnly? Through {get;init;}
    public DateOnly? DueDate {get;init;}
    public string Status {get;init;}="Unreviewed";
    public TaxFilingEvent[] History {get;init;}=[];
    public override string ToString()=>$"{Name} • {Jurisdiction} • {From:yyyy-MM-dd} – {Through:yyyy-MM-dd} • {Status}";
}

public sealed record TaxFilingDecision(string RequestId,int ExpectedRevision,string Status,DateOnly? FilingDate,long? ReportedTaxCents,
    string Reference,string EvidenceId,string Reason);
public sealed record TaxFilingEvent(TaxFilingDecision Decision,string OwnerId,string AtUtc);
