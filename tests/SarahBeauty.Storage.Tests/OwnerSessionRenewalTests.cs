using SarahBeauty.Business.Security;

namespace SarahBeauty.Storage.Tests;

public sealed class OwnerSessionRenewalTests
{
    [Fact]
    public void LockDuringPassphraseConfirmationCannotRenewTheOriginalSession()
    {
        using var f=new BusinessFixture();var access=new OwnerAccess(f.Store){AfterCredentialsRead=f.Owner.Lock};
        Assert.Throws<UnauthorizedAccessException>(()=>access.Confirm(f.Owner,BusinessFixture.Phrase));Assert.Equal(DateTimeOffset.MinValue,f.Owner.ExpiresAt);
        Assert.Throws<UnauthorizedAccessException>(()=>f.Store.BusinessName(f.Owner));
    }
    [Fact]
    public void TouchCannotReviveALockedSession()
    {
        using var f=new BusinessFixture();f.Owner.Lock();Assert.Throws<UnauthorizedAccessException>(()=>f.Owner.Touch());Assert.Equal(DateTimeOffset.MinValue,f.Owner.ExpiresAt);
    }
    [Fact]
    public void FreshSignInAfterLockCreatesNewAccessWithoutRevivingTheOldSession()
    {
        using var f=new BusinessFixture();f.Owner.Lock();var fresh=new OwnerAccess(f.Store).SignIn(f.Owner.OwnerId,BusinessFixture.Phrase);
        Assert.NotEmpty(f.Store.BusinessName(fresh));Assert.Equal(DateTimeOffset.MinValue,f.Owner.ExpiresAt);Assert.Throws<UnauthorizedAccessException>(()=>f.Store.BusinessName(f.Owner));
    }
    [Fact]
    public void NormalConfirmationKeepsTheExistingSessionUsable()
    {
        using var f=new BusinessFixture();new OwnerAccess(f.Store).Confirm(f.Owner,BusinessFixture.Phrase);Assert.NotEmpty(f.Store.BusinessName(f.Owner));
    }
}
