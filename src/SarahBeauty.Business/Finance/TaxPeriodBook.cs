using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

public sealed class TaxPeriodBook(BusinessStore store)
{
    private readonly RecordStore<TaxPeriod> _periods=new(store,"TaxPeriod");
    internal Action? BeforeCommit {get;set;}
    public List<TaxPeriod> List(OwnerSession owner)=>_periods.List(owner);

    public TaxPeriod Save(OwnerSession owner,TaxPeriod value)
    {
        if(string.IsNullOrWhiteSpace(value.Name) || string.IsNullOrWhiteSpace(value.Jurisdiction) || value.From is null || value.Through is null
            || value.Through<value.From || value.DueDate<value.Through)throw new ArgumentException("Enter the period name, jurisdiction, start/end dates, and a due date on or after period end if known.");
        value=value with{Id=value.Id.Length==0?Guid.NewGuid().ToString():value.Id,Name=value.Name.Trim(),Jurisdiction=value.Jurisdiction.Trim()};
        return store.Write(owner,"Save tax period",value.Id,db=>
        {
            var previous=value.Revision==0?null:_periods.Get(db,owner,value.Id);
            if(value.Status!=(previous?.Status??"Unreviewed") || !value.History.SequenceEqual(previous?.History??[]))
                throw new ArgumentException("Record filing status through a reviewed decision. Filing history cannot be overwritten.");
            if(previous?.History.Any(h=>h.Decision.Status is "Filed" or "Amended")==true
                && (value.From!=previous.From || value.Through!=previous.Through || !SameJurisdiction(value,previous)))
                throw new ArgumentException("A period with filing history must retain its jurisdiction and reporting dates.");
            if(_periods.List(db,owner).Any(p=>p.Id!=value.Id && SameJurisdiction(p,value) && p.From<=value.Through && p.Through>=value.From))
                throw new ArgumentException("This jurisdiction already has an overlapping tax period. Update its existing record or filing history.");
            return _periods.Save(db,owner,value);
        });
    }

    public TaxPeriod RecordStatus(OwnerSession owner,string periodId,TaxFilingDecision decision,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        decision=decision with{Reference=decision.Reference.Trim(),EvidenceId=decision.EvidenceId.Trim(),Reason=decision.Reason.Trim()};
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);
            var period=_periods.Get(db,owner,periodId);
            var existing=period.History.SingleOrDefault(h=>h.Decision.RequestId==decision.RequestId);
            if(existing is not null)
            {
                if(existing.Decision!=decision)throw new ArgumentException("This filing request ID already belongs to a different decision.");
                return period;
            }
            if(period.Revision!=decision.ExpectedRevision)throw new ArgumentException("The tax period changed. Reload it before recording status.");
            ValidateDecision(period,decision);
            var saved=_periods.Save(db,owner,period with{Status=decision.Status,History=[..period.History,new(decision,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"))]});
            BusinessStore.Audit(db,owner,"Record tax filing status",periodId);
            BeforeCommit?.Invoke();
            return saved;
        });
    }

    private static void ValidateDecision(TaxPeriod period,TaxFilingDecision decision)
    {
        if(!Guid.TryParse(decision.RequestId,out _) || string.IsNullOrWhiteSpace(decision.Reason)
            || !new[]{"Review required","Not filed","Filed","Amended","Not required"}.Contains(decision.Status))
            throw new ArgumentException("Choose a supported filing status and record its reason.");
        bool filed=decision.Status is "Filed" or "Amended";
        if(filed && (decision.FilingDate is null || decision.FilingDate<period.Through || decision.ReportedTaxCents is null
            || decision.Reference.Length==0 || decision.EvidenceId.Length==0))
            throw new ArgumentException("Record the actual filing date, reported tax (zero or signed refund if applicable), reference, and evidence.");
        if(!filed && (decision.FilingDate is not null || decision.ReportedTaxCents is not null || decision.Reference.Length>0))
            throw new ArgumentException("Filing details belong to a recorded Filed or Amended decision.");
        if(decision.Status=="Amended" && !period.History.Any(h=>h.Decision.Status is "Filed" or "Amended"))
            throw new ArgumentException("Record the original filing before its amendment.");
        if(decision.Status=="Amended" && period.History.Where(h=>h.Decision.Status is "Filed" or "Amended").Any(h=>h.Decision.FilingDate>decision.FilingDate))
            throw new ArgumentException("An amendment date cannot precede the recorded filing or an earlier amendment.");
        if(decision.Status=="Filed" && period.Status is "Filed" or "Amended")throw new ArgumentException("Record an amendment or an explicit review decision for an existing filing.");
        if(decision.Status=="Not required" && decision.EvidenceId.Length==0)throw new ArgumentException("Record evidence for the reviewed decision that no return is required.");
    }

    private static bool SameJurisdiction(TaxPeriod a,TaxPeriod b)=>string.Equals(a.Jurisdiction,b.Jurisdiction,StringComparison.OrdinalIgnoreCase);
}
