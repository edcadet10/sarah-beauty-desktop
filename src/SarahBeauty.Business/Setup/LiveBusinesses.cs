using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Setup;

public sealed record CreatedLiveBusiness(BusinessStore Store,OwnerSession Owner);

public sealed class LiveBusinesses(string parent)
{
    internal Action? BeforePublish {get;set;}

    public CreatedLiveBusiness Create(BusinessProfile profile,string firstName,string firstPhrase,string secondName,string secondPhrase,string reason,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();profile=LiveSetupPolicy.ForCreation(profile);
        var change=BusinessProfileRules.NormalizeChange(new(0,profile,reason));OwnerAccess.RequireSetup(profile.DisplayName,firstName,firstPhrase,secondName,secondPhrase);
        if(new[]{profile.Formation,profile.EinReview,profile.Banking,profile.Tax}.Any(r=>r.Evidence is not null))throw new ArgumentException("Add evidence to the new LIVE business after creation. Files from another business cannot be reused.");
        var stage=new LiveCreationStage(parent);OwnerSession? owner=null;bool published=false;
        try
        {
            var store=new BusinessStore(stage.Database,BusinessMode.Live,allowCreate:true);
            owner=new OwnerAccess(store).Setup(profile.DisplayName,firstName,firstPhrase,secondName,secondPhrase);cancellation.ThrowIfCancellationRequested();
            new BusinessProfiles(store).Confirm(owner,change,firstPhrase,Guid.NewGuid().ToString(),cancellation);new ChartOfAccounts(store).Initialize(owner);
            BeforePublish?.Invoke();stage.Publish(cancellation);published=true;
            return new(new BusinessStore(stage.PublishedDatabase,BusinessMode.Live),owner);
        }
        catch(Exception error)
        {
            owner?.Lock();
            if(published)throw new IOException("The LIVE business was created. Reopen it to sign in; do not create another copy.",error);
            throw;
        }
    }
}
