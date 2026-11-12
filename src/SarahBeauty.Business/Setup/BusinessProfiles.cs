using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Setup;

public sealed partial class BusinessProfiles(BusinessStore store)
{
    public IReadOnlyList<BusinessProfileVersion> History(OwnerSession owner,CancellationToken cancellation=default)=>store.ReadSnapshot(db=>
    {
        cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);var values=BusinessProfileQueries.Read(db,owner.BusinessId);cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);return values;
    });
    public BusinessProfileVersion? Current(OwnerSession owner,CancellationToken cancellation=default)=>History(owner,cancellation).LastOrDefault();
}
