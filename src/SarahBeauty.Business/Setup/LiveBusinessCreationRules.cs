using SarahBeauty.Business.Security;

namespace SarahBeauty.Business.Setup;

public static class LiveBusinessCreationRules
{
    public static BusinessProfileChange Review(BusinessProfile profile,string firstName,string firstPhrase,string secondName,string secondPhrase,string reason)
    {
        profile=LiveSetupPolicy.ForCreation(profile);var change=BusinessProfileRules.NormalizeChange(new(0,profile,reason));OwnerAccess.RequireSetup(profile.DisplayName,firstName,firstPhrase,secondName,secondPhrase);
        if(new[]{profile.Formation,profile.EinReview,profile.Banking,profile.Tax}.Any(r=>r.Evidence is not null))throw new ArgumentException("Add evidence to the new LIVE business after creation. Files from another business cannot be reused.");return change;
    }
}
