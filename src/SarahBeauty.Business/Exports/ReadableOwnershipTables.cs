using SarahBeauty.Business.Ownership;

namespace SarahBeauty.Business.Exports;

internal static class ReadableOwnershipTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget,DateOnly asOf)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;var history=OwnerInterestQueries.Read(db,source.BusinessId);var periods=OwnerInterests.BuildPeriods(history);var current=periods.Select(p=>p.Confirmation.Id).ToHashSet(StringComparer.Ordinal);
        var versions=new ReadableTableBuilder(source,budget,"Ownership confirmations","OwnershipVersionId","BusinessId","Revision","EffectiveFrom","SupersedesOwnershipVersionId","RecordedByOwnerId","RecordedAtUtc","CurrentForEffectiveDate");
        var shares=new ReadableTableBuilder(source,budget,"Ownership confirmed shares","OwnershipVersionId","BusinessId","OwnerId","OwnerNameAtConfirmation","OwnershipFraction");
        foreach(var value in history)
        {
            versions.Add(value.Id,source.BusinessId,value.Revision,value.EffectiveFrom,value.SupersedesId,value.RecordedByOwnerId,value.RecordedAtUtc,current.Contains(value.Id));
            foreach(var share in value.Shares)shares.Add(value.Id,source.BusinessId,share.OwnerId,share.OwnerName,share.Fraction);
        }
        var effective=new ReadableTableBuilder(source,budget,"Ownership effective periods","BusinessId","EffectiveFrom","EffectiveThrough","OwnershipVersionId","Revision");
        foreach(var period in periods)effective.Add(source.BusinessId,period.EffectiveFrom,period.EffectiveThrough,period.Confirmation.Id,period.Confirmation.Revision);
        var selected=periods.SingleOrDefault(p=>p.EffectiveFrom<=asOf && (p.EffectiveThrough is null || asOf<=p.EffectiveThrough));
        var coverage=new ReadableTableBuilder(source,budget,"Ownership coverage","BusinessId","PlanningAsOf","Status","OwnershipVersionId","EffectiveFrom","EffectiveThrough","TotalOwnershipFraction","AllocationPolicy","ObservationScope");
        coverage.Add(source.BusinessId,asOf,selected is null?"Unknown / unconfirmed":"Confirmed",selected?.Confirmation.Id,selected?.EffectiveFrom,selected?.EffectiveThrough,
            selected?.Confirmation.Shares.Sum(s=>s.Fraction),"No automatic cash distribution, taxable-profit allocation, or inferred tax basis.","Captured ownership register, including corrections known at capture. Not ownership frozen in a retained financial report.");
        return new[]{versions.Freeze(),shares.Freeze(),effective.Freeze(),coverage.Freeze()};
    });
}
