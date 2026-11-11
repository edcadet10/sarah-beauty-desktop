using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class OwnerAccessTests
{
    private const string ExamplePhrase = "fictional meadow lantern window";

    [Fact]
    public void SetupAndAuthenticationWorkAfterRestartWithoutNetwork()
    {
        using var fixture = new TestStore();
        var access = new OwnerAccess(new BusinessStore(fixture.DatabasePath));
        Assert.True(access.NeedsSetup);
        var owner = access.Setup("Example Beauty", "Alex", ExamplePhrase, "Morgan", ExamplePhrase);
        var reopened = new OwnerAccess(new BusinessStore(fixture.DatabasePath));
        Assert.False(reopened.NeedsSetup);
        Assert.Equal(2, reopened.Owners().Count);
        Assert.Equal(owner.OwnerId, reopened.SignIn(owner.OwnerId, ExamplePhrase).OwnerId);
        Assert.Throws<UnauthorizedAccessException>(() => reopened.SignIn(owner.OwnerId, "incorrect"));
        Assert.Equal(2L, TestStore.Scalar(fixture.DatabasePath, "SELECT COUNT(DISTINCT Salt) FROM Owners;"));
    }

    [Fact]
    public void FailedAuthenticationIsPersistedAndTemporarilyLocksFurtherAttempts()
    {
        using var fixture = new TestStore();
        var access = new OwnerAccess(new BusinessStore(fixture.DatabasePath));
        var owner = access.Setup("Example", "Alex", ExamplePhrase, "Morgan", ExamplePhrase);
        for (int attempt = 0; attempt < 5; attempt++)
            Assert.Throws<UnauthorizedAccessException>(() => access.SignIn(owner.OwnerId, "incorrect"));
        Assert.Throws<UnauthorizedAccessException>(() => access.SignIn(owner.OwnerId, ExamplePhrase));
        Assert.Equal(5L, TestStore.Scalar(fixture.DatabasePath,
            "SELECT FailedAttempts FROM Owners WHERE Name='Alex';"));
    }

    [Fact]
    public void InvalidOrRepeatedSetupDoesNotReplaceTheOriginalBusiness()
    {
        using var fixture = new TestStore();
        var access = new OwnerAccess(new BusinessStore(fixture.DatabasePath));
        Assert.Throws<ArgumentException>(() => access.Setup("Example", "Alex", ExamplePhrase, "alex", ExamplePhrase));
        Assert.True(access.NeedsSetup);
        access.Setup("Example", "Alex", ExamplePhrase, "Morgan", ExamplePhrase);
        Assert.Throws<InvalidOperationException>(() =>
            access.Setup("Replacement", "Other", ExamplePhrase, "Another", ExamplePhrase));
        Assert.Equal("Example", TestStore.Scalar(fixture.DatabasePath, "SELECT DisplayName FROM Business;"));
    }

    [Fact]
    public void LockedOrCrossStoreSessionsCannotWrite()
    {
        using var first = new TestStore();
        using var second = new TestStore();
        var store = new BusinessStore(first.DatabasePath);
        var other = new BusinessStore(second.DatabasePath);
        var owner = new OwnerAccess(store).Setup("Example", "Alex", ExamplePhrase, "Morgan", ExamplePhrase);
        Assert.Throws<UnauthorizedAccessException>(() => other.Write(owner, "Example", null, _ => true));
        owner.Lock();
        Assert.Throws<UnauthorizedAccessException>(() => store.Write(owner, "Example", null, _ => true));
        Assert.Equal(1L, TestStore.Scalar(first.DatabasePath, "SELECT COUNT(*) FROM Audit;"));
    }

    [Fact]
    public void FailedBusinessWriteDoesNotIncrementWatermarkOrAppendAudit()
    {
        using var fixture = new TestStore();
        var store = new BusinessStore(fixture.DatabasePath);
        var owner = new OwnerAccess(store).Setup("Example", "Alex", ExamplePhrase, "Morgan", ExamplePhrase);
        Assert.Throws<InvalidOperationException>(() => store.Write<bool>(owner, "Example", null, db =>
        {
            db.Execute("UPDATE Business SET DisplayName='Should roll back';");
            throw new InvalidOperationException("Injected failure");
        }));
        Assert.Equal("Example", TestStore.Scalar(fixture.DatabasePath, "SELECT DisplayName FROM Business;"));
        Assert.Equal(1L, TestStore.Scalar(fixture.DatabasePath, "SELECT Watermark FROM Business;"));
    }
}
