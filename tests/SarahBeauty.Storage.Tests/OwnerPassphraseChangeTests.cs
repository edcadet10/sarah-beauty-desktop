using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class OwnerPassphraseChangeTests
{
    [Fact]
    public void ChangeSurvivesReopenRevokesEarlierSessionsAndKeepsTheOtherOwner()
    {
        using var f=new BusinessFixture();var access=new OwnerAccess(f.Store);var earlier=access.SignIn(f.Owner.OwnerId,BusinessFixture.Phrase);string otherId=access.Owners().Single(o=>o.Id!=f.Owner.OwnerId).Id;var other=access.SignIn(otherId,BusinessFixture.Phrase);
        access.ChangePassphrase(f.Owner,BusinessFixture.Phrase,OwnerCredentialBindingTests.NewPhrase,OwnerCredentialBindingTests.NewPhrase);Assert.False(f.Owner.IsActive);Assert.False(access.IsSessionCurrent(earlier));Assert.True(access.IsSessionCurrent(other));
        Assert.Equal(2L,Watermark(f));Assert.Equal(1L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM Audit WHERE Action='Changed own sign-in passphrase';"));Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM JournalLines;"));
        var reopened=new BusinessStore(f.Store.DatabasePath);var freshAccess=new OwnerAccess(reopened);Assert.Throws<UnauthorizedAccessException>(()=>freshAccess.SignIn(f.Owner.OwnerId,BusinessFixture.Phrase));
        var fresh=freshAccess.SignIn(f.Owner.OwnerId,OwnerCredentialBindingTests.NewPhrase);Assert.Equal("Fictional Beauty",reopened.BusinessName(fresh));Assert.Equal("Fictional Beauty",reopened.BusinessName(other));
    }
    [Fact]
    public void WrongCurrentPhraseUsesTheExistingLockoutWithoutChangingCredentialsOrAudit()
    {
        using var f=new BusinessFixture();var access=new OwnerAccess(f.Store);string before=Stamp(f);
        for(int attempt=0;attempt<5;attempt++)Assert.Throws<UnauthorizedAccessException>(()=>access.ChangePassphrase(f.Owner,"fictional incorrect current phrase",OwnerCredentialBindingTests.NewPhrase,OwnerCredentialBindingTests.NewPhrase));
        Assert.True(Stamp(f)==before);Assert.Equal(1L,Watermark(f));Assert.Equal(5L,TestStore.Scalar(f.Store.DatabasePath,"SELECT FailedAttempts FROM Owners WHERE Name='Alex';"));
        Assert.Throws<UnauthorizedAccessException>(()=>access.ChangePassphrase(f.Owner,BusinessFixture.Phrase,OwnerCredentialBindingTests.NewPhrase,OwnerCredentialBindingTests.NewPhrase));Assert.Equal(1L,Watermark(f));
    }
    [Theory]
    [InlineData("short","short")]
    [InlineData("fictional new orchard phrase","fictional mismatched repeat phrase")]
    [InlineData(BusinessFixture.Phrase,BusinessFixture.Phrase)]
    public void InvalidNewPhrasesDoNotCountAsFailedAuthentication(string value,string repeated)
    {
        using var f=new BusinessFixture();Assert.Throws<ArgumentException>(()=>new OwnerAccess(f.Store).ChangePassphrase(f.Owner,BusinessFixture.Phrase,value,repeated));
        Assert.Equal(1L,Watermark(f));Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT SUM(FailedAttempts) FROM Owners;"));Assert.True(new OwnerAccess(f.Store).IsSessionCurrent(f.Owner));
    }
    [Theory]
    [InlineData("cancel")][InlineData("lock")][InlineData("storage")]
    public void FailureAfterCredentialWriteRollsBackCredentialAuditAndWatermark(string defect)
    {
        using var f=new BusinessFixture();using var cancellation=new CancellationTokenSource();string before=Stamp(f);var access=new OwnerAccess(f.Store){BeforePassphraseChangeCommit=()=>
        {
            if(defect=="cancel")cancellation.Cancel();else if(defect=="lock")f.Owner.Lock();else throw new IOException("Fictional interrupted credential write");
        }};
        var error=Record.Exception(()=>access.ChangePassphrase(f.Owner,BusinessFixture.Phrase,OwnerCredentialBindingTests.NewPhrase,OwnerCredentialBindingTests.NewPhrase,cancellation.Token));
        Assert.True(defect switch{"cancel"=>error is OperationCanceledException,"lock"=>error is UnauthorizedAccessException,_=>error is IOException});
        Assert.True(Stamp(f)==before);Assert.Equal(1L,Watermark(f));Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM Audit WHERE Action='Changed own sign-in passphrase';"));
        Assert.Equal("Fictional Beauty",f.Store.BusinessName(new OwnerAccess(f.Store).SignIn(f.Owner.OwnerId,BusinessFixture.Phrase)));
    }
    [Fact]
    public void CompetingPassphraseChangeCannotBeOverwrittenAfterEarlierVerification()
    {
        using var f=new BusinessFixture();var competing=new OwnerAccess(new BusinessStore(f.Store.DatabasePath));var other=competing.SignIn(f.Owner.OwnerId,BusinessFixture.Phrase);const string winning="fictional river orchard lantern";
        var access=new OwnerAccess(f.Store){AfterPassphraseVerified=()=>competing.ChangePassphrase(other,BusinessFixture.Phrase,winning,winning)};
        Assert.Throws<UnauthorizedAccessException>(()=>access.ChangePassphrase(f.Owner,BusinessFixture.Phrase,OwnerCredentialBindingTests.NewPhrase,OwnerCredentialBindingTests.NewPhrase));
        Assert.Equal(2L,Watermark(f));Assert.Equal(1L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM Audit WHERE Action='Changed own sign-in passphrase';"));Assert.Equal("Fictional Beauty",f.Store.BusinessName(competing.SignIn(f.Owner.OwnerId,winning)));
    }
    [Fact]
    public void CancellationAndForeignAccessCannotBeginAChange()
    {
        using var f=new BusinessFixture();using var foreign=new BusinessFixture();using var cancellation=new CancellationTokenSource();cancellation.Cancel();var access=new OwnerAccess(f.Store);
        Assert.Throws<OperationCanceledException>(()=>access.ChangePassphrase(f.Owner,BusinessFixture.Phrase,OwnerCredentialBindingTests.NewPhrase,OwnerCredentialBindingTests.NewPhrase,cancellation.Token));
        Assert.Throws<UnauthorizedAccessException>(()=>access.ChangePassphrase(foreign.Owner,BusinessFixture.Phrase,OwnerCredentialBindingTests.NewPhrase,OwnerCredentialBindingTests.NewPhrase));Assert.Equal(1L,Watermark(f));Assert.True(access.IsSessionCurrent(f.Owner));
    }
    private static long Watermark(BusinessFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));
    private static string Stamp(BusinessFixture f)=>f.Store.Read(db=>db.Read("SELECT Salt,Hash,Iterations FROM Owners WHERE Id=$owner;",r=>OwnerCredentials.Stamp(r.GetString(0),r.GetString(1),r.GetInt32(2)),("$owner",f.Owner.OwnerId)).Single());
}
