using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Ownership;

public sealed record OwnerInterestOwner(string Id,string Name,bool LoginActive);
public sealed record OwnerInterestPeriod(DateOnly EffectiveFrom,DateOnly? EffectiveThrough,OwnerInterestVersion Confirmation);
public sealed partial class OwnerInterests(BusinessStore store)
{
    public IReadOnlyList<OwnerInterestOwner> Owners(OwnerSession owner)=>store.ReadSnapshot(db=>{store.RequireSession(db,owner);return OwnerInterestQueries.Owners(db,owner.BusinessId);});
    public IReadOnlyList<OwnerInterestVersion> History(OwnerSession owner)=>store.ReadSnapshot(db=>{store.RequireSession(db,owner);return OwnerInterestQueries.Read(db,owner.BusinessId);});
    public IReadOnlyList<OwnerInterestPeriod> Periods(OwnerSession owner)=>BuildPeriods(History(owner));
    public OwnerInterestPeriod? At(OwnerSession owner,DateOnly date)=>Periods(owner).SingleOrDefault(p=>p.EffectiveFrom<=date && (p.EffectiveThrough is null || date<=p.EffectiveThrough));
    internal static IReadOnlyList<OwnerInterestPeriod> BuildPeriods(IReadOnlyList<OwnerInterestVersion> history)
    {
        var superseded=history.Where(v=>v.SupersedesId is not null).Select(v=>v.SupersedesId).ToHashSet(StringComparer.Ordinal);
        var current=history.Where(v=>!superseded.Contains(v.Id)).OrderBy(v=>v.EffectiveFrom).ToArray();
        return Array.AsReadOnly(current.Select((v,i)=>new OwnerInterestPeriod(v.EffectiveFrom,i+1<current.Length?current[i+1].EffectiveFrom.AddDays(-1):null,v)).ToArray());
    }
}
