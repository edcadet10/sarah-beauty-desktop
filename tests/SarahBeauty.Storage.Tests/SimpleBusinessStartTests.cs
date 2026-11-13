using SarahBeauty.Business.Security;
using SarahBeauty.Business.Setup;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class SimpleBusinessStartTests
{
    [Fact]
    public void ThreeInputsCreateOneUserAndReopenWithOtherFactsUnknown()
    {
        using var files = new TestStore();
        var businesses = new LiveBusinesses(Path.Combine(files.Folder, "Live"));
        var created = businesses.Start(" Fictional Beauty ", " fictional-user ", BusinessFixture.Phrase);
        Assert.Equal("Fictional Beauty", created.Store.BusinessName(created.Owner));
        Assert.Single(new OwnerAccess(created.Store).Owners());
        var profile = new BusinessProfiles(created.Store).Current(created.Owner)!.Profile;
        Assert.Equal(new BusinessProfile { DisplayName = "Fictional Beauty" }, profile);
        created.Owner.Lock();
        var reopened = businesses.Open();
        var session = new OwnerAccess(reopened).SignIn(created.Owner.OwnerId, BusinessFixture.Phrase);
        Assert.Equal("fictional-user", session.OwnerName);
        Assert.Equal(BusinessMode.Live, session.Mode);
        Assert.Throws<UnauthorizedAccessException>(() => new OwnerAccess(reopened).SignIn(session.OwnerId, "incorrect"));
    }

    [Theory]
    [InlineData("", "example", "fictional meadow lantern window")]
    [InlineData("Example", " ", "fictional meadow lantern window")]
    [InlineData("Example", "example\nuser", "fictional meadow lantern window")]
    [InlineData("Example", "example", "short")]
    public void InvalidInputsCreateNoDestination(string name, string username, string password)
    {
        using var files = new TestStore();
        string root = Path.Combine(files.Folder, "Live");
        Assert.Throws<ArgumentException>(() => new LiveBusinesses(root).Start(name, username, password));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void ExistingBusinessIsPreservedWhenStartIsRepeated()
    {
        using var files = new TestStore();
        var businesses = new LiveBusinesses(Path.Combine(files.Folder, "Live"));
        var original = businesses.Start("Original", "example", BusinessFixture.Phrase);
        Assert.Throws<IOException>(() => businesses.Start("Replacement", "other", BusinessFixture.Phrase));
        Assert.Equal(original.Store.StoreId, businesses.Open().StoreId);
        Assert.Equal("Original", original.Store.BusinessName(original.Owner));
    }

    [Fact]
    public void SecondUserCanBeAddedLaterWithCurrentPassword()
    {
        using var files = new TestStore();
        var businesses = new LiveBusinesses(Path.Combine(files.Folder, "Live"));
        var created = businesses.Start("Example", "first", BusinessFixture.Phrase);
        var access = new OwnerAccess(created.Store);
        Assert.Throws<UnauthorizedAccessException>(() => access.AddUser(created.Owner, "incorrect", "second", BusinessFixture.Phrase));
        Assert.Single(access.Owners());
        Assert.Throws<ArgumentException>(() => access.AddUser(created.Owner, BusinessFixture.Phrase, "FIRST", BusinessFixture.Phrase));
        string second = access.AddUser(created.Owner, BusinessFixture.Phrase, "second", BusinessFixture.Phrase);
        Assert.Equal("second", access.SignIn(second, BusinessFixture.Phrase).OwnerName);
        Assert.Equal(2, new OwnerAccess(businesses.Open()).Owners().Count);
        Assert.Throws<ArgumentException>(() => access.AddUser(created.Owner, BusinessFixture.Phrase, "third", BusinessFixture.Phrase));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptedStartDoesNotPublishAnIncompleteBusiness(bool cancel)
    {
        using var files = new TestStore();
        using var cancellation = new CancellationTokenSource();
        string root = Path.Combine(files.Folder, "Live");
        var businesses = new LiveBusinesses(root) { BeforePublish = () =>
        {
            if (cancel) cancellation.Cancel(); else throw new IOException("Fictional interruption");
        }};
        if (cancel) Assert.Throws<OperationCanceledException>(() => businesses.Start("Example", "first", BusinessFixture.Phrase, cancellation.Token));
        else Assert.Throws<IOException>(() => businesses.Start("Example", "first", BusinessFixture.Phrase));
        Assert.False(businesses.Exists);
        Assert.False(Directory.Exists(Path.Combine(root, "Primary")));
    }
}
