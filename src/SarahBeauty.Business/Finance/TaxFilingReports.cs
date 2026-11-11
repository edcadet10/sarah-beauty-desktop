using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class TaxFilingReports
{
    internal static TaxFilingSnapshot Read(DbWork db,BusinessStore store,OwnerSession owner,DateOnly from,DateOnly through)
    {
        var names=db.Read("SELECT Id,Name FROM Owners WHERE BusinessId=$business;",r=>(Id:r.GetString(0),Name:r.GetString(1)),("$business",owner.BusinessId)).ToDictionary(o=>o.Id,o=>o.Name);
        var periods=new RecordStore<TaxPeriod>(store,"TaxPeriod").List(db,owner).Where(p=>p.From<=through && p.Through>=from).OrderBy(p=>p.From).ThenBy(p=>p.Jurisdiction);
        return new(1,periods.Select(p=>new TaxFilingPeriodSnapshot(p.Id,p.Name,p.Jurisdiction,p.From,p.Through,p.DueDate,p.Status,p.Archived,p.Revision,
            p.History.Select(h=>new TaxFilingReview(h.Decision,h.OwnerId,names.GetValueOrDefault(h.OwnerId,"Former owner"),h.AtUtc)).ToArray())).ToList());
    }
}
