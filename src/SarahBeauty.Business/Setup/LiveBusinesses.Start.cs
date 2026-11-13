using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Setup;

public sealed partial class LiveBusinesses
{
    public CreatedLiveBusiness Start(string businessName, string username, string password, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        OwnerAccess.RequireStart(businessName, username, password);
        var stage = new LiveCreationStage(parent);
        OwnerSession? owner = null;
        bool published = false;
        try
        {
            var store = new BusinessStore(stage.Database, BusinessMode.Live, allowCreate: true);
            owner = new OwnerAccess(store).Start(businessName, username, password);
            var profile = new BusinessProfile { DisplayName = businessName.Trim() };
            new BusinessProfiles(store).Confirm(owner, new(0, profile, "Initial business name"), password, Guid.NewGuid().ToString(), cancellation);
            new ChartOfAccounts(store).Initialize(owner);
            BeforePublish?.Invoke();
            stage.Publish(cancellation);
            published = true;
            return new(new BusinessStore(stage.PublishedDatabase, BusinessMode.Live), owner);
        }
        catch (Exception error)
        {
            owner?.Lock();
            if (published) throw new IOException("Your business was created. Reopen the app and sign in.", error);
            throw;
        }
    }
}
