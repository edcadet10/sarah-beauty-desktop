using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Bookings;

public sealed record ServiceDefinition : BusinessRecord
{
    public string Code {get;init;}="";
    public string Unit {get;init;}="Custom";
    public string Kind {get;init;}="Service";
    public long? UnitCents {get;init;}
    public int? DurationMinutes {get;init;}
    public string TaxReviewStatus {get;init;}="Unreviewed";
    public string TaxReviewEvidence {get;init;}="";
    public ServiceRevision[] History {get;init;}=[];
}

public sealed record ServiceRevision(int Revision,string Name,string Unit,string Kind,long UnitCents,int? DurationMinutes,bool Archived,string OwnerId,string AtUtc)
{
    public string TaxReviewStatus {get;init;}="Unreviewed";
    public string TaxReviewEvidence {get;init;}="";
}
