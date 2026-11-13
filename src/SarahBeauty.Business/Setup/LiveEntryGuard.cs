using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Setup;

internal static class LiveEntryGuard
{
    internal static void Require(DbWork db,OwnerSession owner,DateOnly date)
    {
        if(owner.Mode!=BusinessMode.Live)return;
        var profile=BusinessProfileQueries.Read(db,owner.BusinessId).LastOrDefault()?.Profile??throw new ArgumentException("Review the LIVE business setup before issuing or changing operational records.");
        LiveSetupPolicy.RequireOperation(profile,date,TimeProvider.System.GetUtcNow());
    }
}
