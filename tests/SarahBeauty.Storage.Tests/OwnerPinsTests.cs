using System.Security.Cryptography;
using System.Text;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class OwnerPinsTests
{
    private static string Pin=>string.Concat(Enumerable.Range(0,6));
    private static string OtherPin=>string.Concat(Enumerable.Range(3,6));
    private const string NewPhrase="Fictional changed owner pin phrase";

    [Fact]
    public void BothOwnersKeepDistinctDevicePinsAndPasswordsWithoutChangingBusinessFacts()
    {
        using var f=new BusinessFixture();using var privateFiles=new TestStore();var access=new OwnerAccess(f.Store);var pins=new OwnerPins(f.Store,privateFiles.Folder);
        var second=access.Owners().Single(o=>o.Id!=f.Owner.OwnerId);var other=access.SignIn(second.Id,BusinessFixture.Phrase);long mark=Watermark(f);
        long audits=Convert.ToInt64(TestStore.Scalar(f.Files.DatabasePath,"SELECT count(*) FROM Audit;"));
        Assert.False(pins.IsConfigured(f.Owner.OwnerId));pins.Set(f.Owner,BusinessFixture.Phrase,Pin,Pin);pins.Set(other,BusinessFixture.Phrase,OtherPin,OtherPin);
        Assert.True(pins.IsConfigured(f.Owner.OwnerId));Assert.Equal(f.Owner.OwnerId,pins.SignIn(f.Owner.OwnerId,Pin).OwnerId);Assert.Equal(other.OwnerId,pins.SignIn(other.OwnerId,OtherPin).OwnerId);
        Assert.Throws<UnauthorizedAccessException>(()=>pins.SignIn(other.OwnerId,Pin));Assert.Equal(other.OwnerId,access.SignIn(other.OwnerId,BusinessFixture.Phrase).OwnerId);
        Assert.Equal(mark,Watermark(f));Assert.Equal(audits,Convert.ToInt64(TestStore.Scalar(f.Files.DatabasePath,"SELECT count(*) FROM Audit;")));
    }

    [Theory]
    [InlineData("")][InlineData("short")][InlineData("12345")][InlineData("1234567890123")][InlineData("１２３４５６")][InlineData("123 456")]
    public void InvalidOrMismatchedPinsDoNotReplaceTheCurrentPin(string invalid)
    {
        using var f=new BusinessFixture();using var root=new TestStore();var pins=new OwnerPins(f.Store,root.Folder);pins.Set(f.Owner,BusinessFixture.Phrase,Pin,Pin);
        Assert.Throws<ArgumentException>(()=>pins.Set(f.Owner,BusinessFixture.Phrase,invalid,invalid));
        Assert.Throws<ArgumentException>(()=>pins.Set(f.Owner,BusinessFixture.Phrase,OtherPin,Pin));Assert.Equal(f.Owner.OwnerId,pins.SignIn(f.Owner.OwnerId,Pin).OwnerId);
    }

    [Fact]
    public void ConfiguringOrRemovingRequiresTheCurrentOwnersPassword()
    {
        using var f=new BusinessFixture();using var root=new TestStore();var pins=new OwnerPins(f.Store,root.Folder);pins.Set(f.Owner,BusinessFixture.Phrase,Pin,Pin);
        Assert.Throws<UnauthorizedAccessException>(()=>pins.Set(f.Owner,NewPhrase,OtherPin,OtherPin));Assert.Throws<UnauthorizedAccessException>(()=>pins.Remove(f.Owner,NewPhrase));
        Assert.Equal(f.Owner.OwnerId,pins.SignIn(f.Owner.OwnerId,Pin).OwnerId);pins.Remove(f.Owner,BusinessFixture.Phrase);Assert.False(pins.IsConfigured(f.Owner.OwnerId));
        Assert.Throws<UnauthorizedAccessException>(()=>pins.SignIn(f.Owner.OwnerId,Pin));Assert.Equal(f.Owner.OwnerId,new OwnerAccess(f.Store).SignIn(f.Owner.OwnerId,BusinessFixture.Phrase).OwnerId);
    }

    [Fact]
    public void PasswordAndPinFailuresShareTheSameLockout()
    {
        using var f=new BusinessFixture();using var root=new TestStore();var pins=new OwnerPins(f.Store,root.Folder);var access=new OwnerAccess(f.Store);pins.Set(f.Owner,BusinessFixture.Phrase,Pin,Pin);
        for(int n=0;n<2;n++)Assert.Throws<UnauthorizedAccessException>(()=>pins.SignIn(f.Owner.OwnerId,OtherPin));
        for(int n=0;n<3;n++)Assert.Throws<UnauthorizedAccessException>(()=>access.SignIn(f.Owner.OwnerId,NewPhrase));
        Assert.Throws<UnauthorizedAccessException>(()=>pins.SignIn(f.Owner.OwnerId,Pin));Assert.Throws<UnauthorizedAccessException>(()=>access.SignIn(f.Owner.OwnerId,BusinessFixture.Phrase));
        Assert.Equal(5,Convert.ToInt32(TestStore.Scalar(f.Files.DatabasePath,"SELECT FailedAttempts FROM Owners WHERE Id='"+f.Owner.OwnerId+"';")));
        f.Files.Execute("UPDATE Owners SET LockedUntil='2000-01-01T00:00:00Z';");Assert.Equal(f.Owner.OwnerId,pins.SignIn(f.Owner.OwnerId,Pin).OwnerId);
        Assert.Equal(0,Convert.ToInt32(TestStore.Scalar(f.Files.DatabasePath,"SELECT FailedAttempts FROM Owners WHERE Id='"+f.Owner.OwnerId+"';")));
    }

    [Fact]
    public void PasswordChangesInvalidateOldPinsAndStaleSetupSessions()
    {
        using var f=new BusinessFixture();using var root=new TestStore();var pins=new OwnerPins(f.Store,root.Folder);var access=new OwnerAccess(f.Store);pins.Set(f.Owner,BusinessFixture.Phrase,Pin,Pin);
        var old=access.SignIn(f.Owner.OwnerId,BusinessFixture.Phrase);access.ChangePassphrase(f.Owner,BusinessFixture.Phrase,NewPhrase,NewPhrase);
        Assert.False(pins.IsConfigured(old.OwnerId));Assert.Throws<UnauthorizedAccessException>(()=>pins.SignIn(old.OwnerId,Pin));
        Assert.Throws<UnauthorizedAccessException>(()=>pins.Set(old,NewPhrase,OtherPin,OtherPin));var current=access.SignIn(old.OwnerId,NewPhrase);
        pins.Set(current,NewPhrase,OtherPin,OtherPin);Assert.Equal(old.OwnerId,pins.SignIn(old.OwnerId,OtherPin).OwnerId);
    }

    [Fact]
    public void ChangedCredentialsDuringPinReviewCannotAuthenticateTheStaleOwner()
    {
        using var f=new BusinessFixture();using var root=new TestStore();var pins=new OwnerPins(f.Store,root.Folder);pins.Set(f.Owner,BusinessFixture.Phrase,Pin,Pin);
        var access=new OwnerAccess(f.Store){AfterCredentialsRead=()=>new OwnerAccess(f.Store).ChangePassphrase(f.Owner,BusinessFixture.Phrase,NewPhrase,NewPhrase)};
        Assert.Throws<UnauthorizedAccessException>(()=>access.SignInWithPin(f.Owner.OwnerId,Pin,pins));Assert.False(pins.IsConfigured(f.Owner.OwnerId));
    }

    [Fact]
    public void CopiedOrDamagedProtectedFilesDoNotUnlockAnotherOwnerOrBusiness()
    {
        using var f=new BusinessFixture();using var other=new BusinessFixture();using var root=new TestStore();var pins=new OwnerPins(f.Store,root.Folder);pins.Set(f.Owner,BusinessFixture.Phrase,Pin,Pin);
        var access=new OwnerAccess(f.Store);var second=access.Owners().Single(o=>o.Id!=f.Owner.OwnerId);var owner=access.SignIn(second.Id,BusinessFixture.Phrase);pins.Set(owner,BusinessFixture.Phrase,OtherPin,OtherPin);
        string source=PathFor(f.Store,root.Folder,f.Owner.OwnerId),target=PathFor(f.Store,root.Folder,owner.OwnerId);File.Copy(source,target,true);
        Assert.Throws<UnauthorizedAccessException>(()=>pins.SignIn(owner.OwnerId,Pin));Assert.Equal(f.Owner.OwnerId,pins.SignIn(f.Owner.OwnerId,Pin).OwnerId);
        var foreign=new OwnerPins(other.Store,root.Folder);foreign.Set(other.Owner,BusinessFixture.Phrase,OtherPin,OtherPin);string destination=PathFor(other.Store,root.Folder,other.Owner.OwnerId);File.Copy(source,destination,true);
        Assert.False(foreign.IsConfigured(other.Owner.OwnerId));Assert.Throws<UnauthorizedAccessException>(()=>foreign.SignIn(other.Owner.OwnerId,Pin));
        byte[] damaged=File.ReadAllBytes(source);damaged[^1]^=1;File.WriteAllBytes(source,damaged);Assert.False(pins.IsConfigured(f.Owner.OwnerId));Assert.Throws<UnauthorizedAccessException>(()=>pins.SignIn(f.Owner.OwnerId,Pin));
    }

    [Fact]
    public void ProtectedPinMaterialStaysOutsideArchivesAndAConvertedPinSessionStillNeedsPasswordConfirmation()
    {
        using var f=new BusinessFixture();using var root=new TestStore();var pins=new OwnerPins(f.Store,root.Folder);pins.Set(f.Owner,BusinessFixture.Phrase,Pin,Pin);
        string file=PathFor(f.Store,root.Folder,f.Owner.OwnerId);Assert.Equal(-1,File.ReadAllBytes(file).AsSpan().IndexOf(Encoding.UTF8.GetBytes(Pin)));
        var session=pins.SignIn(f.Owner.OwnerId,Pin);var access=new OwnerAccess(f.Store);Assert.Throws<UnauthorizedAccessException>(()=>access.Confirm(session,Pin));access.Confirm(session,BusinessFixture.Phrase);
        using var captured=new SarahBeauty.Business.Recovery.ArchiveSources(f.Store).Capture(session);Assert.Single(captured.Manifest.Files);
        Assert.DoesNotContain(captured.Manifest.Files,x=>x.RelativePath.Contains("OwnerPin",StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CancellationAndLockedOwnersCannotReplaceAWorkingPin()
    {
        using var f=new BusinessFixture();using var root=new TestStore();var pins=new OwnerPins(f.Store,root.Folder);pins.Set(f.Owner,BusinessFixture.Phrase,Pin,Pin);
        using var cancel=new CancellationTokenSource();cancel.Cancel();Assert.Throws<OperationCanceledException>(()=>pins.Set(f.Owner,BusinessFixture.Phrase,OtherPin,OtherPin,cancel.Token));
        f.Owner.Lock();Assert.Throws<UnauthorizedAccessException>(()=>pins.Remove(f.Owner,BusinessFixture.Phrase));Assert.Equal(f.Owner.OwnerId,pins.SignIn(f.Owner.OwnerId,Pin).OwnerId);
    }

    [Fact]
    public void ANewWritableInstanceRequiresPasswordAndItsOwnPinSetup()
    {
        using var f=new BusinessFixture();using var root=new TestStore();var pins=new OwnerPins(f.Store,root.Folder);pins.Set(f.Owner,BusinessFixture.Phrase,Pin,Pin);
        string original=PathFor(f.Store,root.Folder,f.Owner.OwnerId);f.Files.Execute($"UPDATE AppMetadata SET Value='{Guid.NewGuid():N}' WHERE Key='WritableInstanceId';");
        var current=new BusinessStore(f.Files.DatabasePath);var currentPins=new OwnerPins(current,root.Folder);Assert.False(currentPins.IsConfigured(f.Owner.OwnerId));
        Assert.Throws<UnauthorizedAccessException>(()=>currentPins.SignIn(f.Owner.OwnerId,Pin));var owner=new OwnerAccess(current).SignIn(f.Owner.OwnerId,BusinessFixture.Phrase);
        currentPins.Set(owner,BusinessFixture.Phrase,OtherPin,OtherPin);Assert.Equal(owner.OwnerId,currentPins.SignIn(owner.OwnerId,OtherPin).OwnerId);Assert.True(File.Exists(original));
    }

    private static long Watermark(BusinessFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Files.DatabasePath,"SELECT Watermark FROM Business;"));
    private static string PathFor(BusinessStore store,string root,string owner)=>Path.Combine(new DeviceCredentialFiles(store,Path.Combine(root,owner)).DirectoryPath,"OwnerPin.dat");
}
