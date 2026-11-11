using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Security;

namespace SarahBeauty.Storage.Tests;

public sealed class OwnerAuthenticationConcurrencyTests
{
    [Fact]
    public async Task VerifyingAPassphraseDoesNotReserveTheBusinessWriter()
    {
        using var f=new BusinessFixture();using var read=new ManualResetEventSlim();using var resume=new ManualResetEventSlim();
        var access=new OwnerAccess(f.Store){AfterCredentialsRead=()=>{read.Set();if(!resume.Wait(TimeSpan.FromSeconds(20)))throw new TimeoutException("Fictional verification gate timed out.");}};
        var authentication=Task.Run(()=>access.SignIn(f.Owner.OwnerId,BusinessFixture.Phrase));
        try
        {
            Assert.True(read.Wait(TimeSpan.FromSeconds(10)));var saved=new CustomerBook(f.Store).Save(f.Owner,new(){FullName="Fictional write during verification"});
            Assert.NotEmpty(saved.Id);
        }
        finally{resume.Set();await authentication;}
        Assert.Single(new CustomerBook(f.Store).Search(f.Owner));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedCredentialsOrDeactivatedOwnerCannotUseTheEarlierVerification(bool deactivate)
    {
        using var f=new BusinessFixture();const string replacement="fictional alternate meadow lantern";var changed=Passphrase.Hash(replacement);
        var access=new OwnerAccess(f.Store){AfterCredentialsRead=()=>f.Store.Transaction(db=>deactivate
            ?db.Execute("UPDATE Owners SET Active=0 WHERE Id=$id;",("$id",f.Owner.OwnerId))
            :db.Execute("UPDATE Owners SET Salt=$salt,Hash=$hash WHERE Id=$id;",("$salt",changed.Salt),("$hash",changed.Hash),("$id",f.Owner.OwnerId)))};
        Assert.Throws<UnauthorizedAccessException>(()=>access.SignIn(f.Owner.OwnerId,BusinessFixture.Phrase));
        if(!deactivate)Assert.Equal(f.Owner.OwnerId,new OwnerAccess(f.Store).SignIn(f.Owner.OwnerId,replacement).OwnerId);
        Assert.Equal(0L,TestStore.Scalar(f.Files.DatabasePath,"SELECT FailedAttempts FROM Owners WHERE Name='Alex';"));
    }

    [Fact]
    public async Task ConcurrentFailuresUseCurrentCountersAndPersistTheLockout()
    {
        using var f=new BusinessFixture();using var ready=new CountdownEvent(5);using var resume=new ManualResetEventSlim();
        var access=new OwnerAccess(f.Store){AfterCredentialsRead=()=>{ready.Signal();if(!resume.Wait(TimeSpan.FromSeconds(20)))throw new TimeoutException("Fictional verification gate timed out.");}};
        var attempts=Enumerable.Range(0,5).Select(_=>Task.Factory.StartNew(()=>Assert.Throws<UnauthorizedAccessException>(()=>access.SignIn(f.Owner.OwnerId,"fictional incorrect phrase")),CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default)).ToArray();
        try{Assert.True(ready.Wait(TimeSpan.FromSeconds(10)));}finally{resume.Set();await Task.WhenAll(attempts);}
        Assert.Equal(5L,TestStore.Scalar(f.Files.DatabasePath,"SELECT FailedAttempts FROM Owners WHERE Name='Alex';"));
        Assert.Throws<UnauthorizedAccessException>(()=>new OwnerAccess(new SarahBeauty.Business.Storage.BusinessStore(f.Files.DatabasePath)).SignIn(f.Owner.OwnerId,BusinessFixture.Phrase));
    }

    [Fact]
    public void ALockoutRecordedDuringVerificationCannotBeClearedByItsEarlierValidProof()
    {
        using var f=new BusinessFixture();var access=new OwnerAccess(f.Store){AfterCredentialsRead=()=>
        {
            var other=new OwnerAccess(f.Store);for(int i=0;i<5;i++)Assert.Throws<UnauthorizedAccessException>(()=>other.SignIn(f.Owner.OwnerId,"fictional incorrect phrase"));
        }};
        Assert.Throws<UnauthorizedAccessException>(()=>access.SignIn(f.Owner.OwnerId,BusinessFixture.Phrase));
        Assert.Equal(5L,TestStore.Scalar(f.Files.DatabasePath,"SELECT FailedAttempts FROM Owners WHERE Name='Alex';"));
    }
}
