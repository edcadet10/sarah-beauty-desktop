using SarahBeauty.Business.Security;

namespace SarahBeauty.Storage.Tests;

public sealed class OwnerCredentialBindingTests
{
    internal const string NewPhrase="fictional orchard lantern meadow";
    [Fact]
    public void ReplacedCredentialsRejectExistingSessionsButAllowFreshSignIn()
    {
        using var f=new BusinessFixture();var access=new OwnerAccess(f.Store);var second=access.SignIn(f.Owner.OwnerId,BusinessFixture.Phrase);Replace(f);
        Assert.False(access.IsSessionCurrent(f.Owner));Assert.False(f.Owner.IsActive);Assert.Throws<UnauthorizedAccessException>(()=>f.Store.BusinessName(second));
        var fresh=access.SignIn(f.Owner.OwnerId,NewPhrase);Assert.Equal("Fictional Beauty",f.Store.BusinessName(fresh));Assert.Throws<UnauthorizedAccessException>(()=>access.SignIn(f.Owner.OwnerId,BusinessFixture.Phrase));
    }
    [Fact]
    public void ConfirmationWithTheNewPhraseCannotReviveTheEarlierCredentialSession()
    {
        using var f=new BusinessFixture();Replace(f);Assert.Throws<UnauthorizedAccessException>(()=>new OwnerAccess(f.Store).Confirm(f.Owner,NewPhrase));Assert.False(f.Owner.IsActive);
    }
    [Fact]
    public void OwnerIdentityWithoutItsAuthenticatedCredentialBindingCannotReadPrivateData()
    {
        using var f=new BusinessFixture();var unbound=new OwnerSession(f.Owner.OwnerId,f.Owner.OwnerName,f.Owner.BusinessId,f.Store.StoreId,f.Store.InstanceId);
        Assert.Throws<UnauthorizedAccessException>(()=>f.Store.BusinessName(unbound));Assert.False(unbound.IsActive);Assert.True(new OwnerAccess(f.Store).IsSessionCurrent(f.Owner));
        Assert.DoesNotContain(f.Owner.CredentialStamp!,System.Text.Json.JsonSerializer.Serialize(f.Owner));
    }
    private static void Replace(BusinessFixture f)
    {
        var credential=Passphrase.Hash(NewPhrase);f.Store.Transaction(db=>db.Execute("UPDATE Owners SET Salt=$salt,Hash=$hash,Iterations=$iterations WHERE Id=$owner;",("$salt",credential.Salt),("$hash",credential.Hash),("$iterations",Passphrase.Iterations),("$owner",f.Owner.OwnerId)));
    }
}
