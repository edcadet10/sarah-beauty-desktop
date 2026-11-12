using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Setup;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class OwnerModeTests
{
    [Theory]
    [InlineData("read")][InlineData("activity")][InlineData("confirmation")][InlineData("profile")]
    public void MatchingIdsAndCredentialCannotAuthorizeASessionFromAnotherMode(string operation)
    {
        using var f=new BusinessFixture();var foreignMode=new OwnerSession(f.Owner.OwnerId,f.Owner.OwnerName,f.Owner.BusinessId,f.Owner.StoreId,f.Owner.InstanceId,credentialStamp:f.Owner.CredentialStamp,mode:BusinessMode.Live);var access=new OwnerAccess(f.Store);
        if(operation=="activity"){Assert.False(access.IsSessionCurrent(foreignMode));Assert.False(access.TryRecordActivity(foreignMode));}
        else Assert.Throws<UnauthorizedAccessException>(()=>{if(operation=="read")f.Store.BusinessName(foreignMode);else if(operation=="confirmation")access.Confirm(foreignMode,BusinessFixture.Phrase);else new BusinessProfiles(f.Store).Confirm(foreignMode,BusinessProfileConfirmTests.Draft(),BusinessFixture.Phrase,Guid.NewGuid().ToString());});
        Assert.True(foreignMode.IsActive);Assert.True(f.Owner.IsActive);Assert.Equal("Fictional Beauty",f.Store.BusinessName(f.Owner));Assert.Equal(1L,TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM BusinessProfileVersions;"));
    }
    [Fact]
    public void SetupAndSignInBindTheStoreModeWithoutSerializingTheInternalBinding()
    {
        using var f=new BusinessFixture();Assert.Equal(BusinessMode.Test,f.Store.Mode);Assert.Equal(f.Store.Mode,f.Owner.Mode);var signedIn=new OwnerAccess(f.Store).SignIn(f.Owner.OwnerId,BusinessFixture.Phrase);Assert.Equal(f.Store.Mode,signedIn.Mode);Assert.DoesNotContain("\"Mode\"",JsonSerializer.Serialize(signedIn));Assert.DoesNotContain("CredentialStamp",JsonSerializer.Serialize(signedIn));
    }
    [Fact]
    public void UndefinedSessionModeIsRejected()=>Assert.Throws<ArgumentOutOfRangeException>(()=>new OwnerSession("Fictional owner","Fictional owner","Fictional business","Fictional store","Fictional instance",mode:(BusinessMode)99));
}
