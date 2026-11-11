using System.Security.Cryptography;
using SarahBeauty.Business.Recovery;

namespace SarahBeauty.Storage.Tests;

public sealed class RecoveryCipherTests
{
    [Fact]
    public async Task RealHelperProtectsRecoveryAndPublishesOnlyVerifiedStreamingOutput()
    {
        using var f=new TestStore();var cipher=new RecoveryCipher();var material=await cipher.CreateAsync(BusinessFixture.Phrase);
        Assert.Equal(material.Recipient,await cipher.InspectAsync(material.ProtectedIdentity,BusinessFixture.Phrase));
        byte[] bytes=Enumerable.Range(0,180000).Select(i=>(byte)(i%251)).ToArray();string source=Path.Combine(f.Folder,"fictional-source.bin"),encrypted=Path.Combine(f.Folder,"fictional.age"),restored=Path.Combine(f.Folder,"restored.bin");
        await File.WriteAllBytesAsync(source,bytes);var saved=await cipher.EncryptAsync(source,encrypted,material.Recipient);
        Assert.Equal(new FileInfo(encrypted).Length,saved.ByteLength);Assert.Equal(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(encrypted))),saved.Sha256);
        var recovered=await cipher.DecryptAsync(encrypted,restored,material.ProtectedIdentity,BusinessFixture.Phrase);Assert.Equal(bytes,await File.ReadAllBytesAsync(restored));Assert.Equal(bytes.Length,recovered.ByteLength);
        Assert.Empty(Directory.EnumerateDirectories(f.Folder,".sarah-recovery-*"));
    }

    [Fact]
    public async Task WrongPassphraseAndTruncatedFileNeverPublishPlaintextOrEchoPrivateInput()
    {
        using var f=new TestStore();var cipher=new RecoveryCipher();var material=await cipher.CreateAsync(BusinessFixture.Phrase);
        string source=Path.Combine(f.Folder,"fictional-source.bin"),encrypted=Path.Combine(f.Folder,"fictional.age"),restored=Path.Combine(f.Folder,"must-not-publish.bin");await File.WriteAllBytesAsync(source,new byte[150000]);await cipher.EncryptAsync(source,encrypted,material.Recipient);
        const string wrong="fictional alternate private phrase";var failure=await Assert.ThrowsAsync<RecoveryFailure>(()=>cipher.DecryptAsync(encrypted,restored,material.ProtectedIdentity,wrong));
        Assert.Equal("AuthenticationFailed",failure.Code);Assert.DoesNotContain(wrong,failure.ToString());Assert.False(File.Exists(restored));
        byte[] contents=await File.ReadAllBytesAsync(encrypted);await File.WriteAllBytesAsync(encrypted,contents[..^1]);
        await Assert.ThrowsAsync<RecoveryFailure>(()=>cipher.DecryptAsync(encrypted,restored,material.ProtectedIdentity,BusinessFixture.Phrase));Assert.False(File.Exists(restored));Assert.Empty(Directory.EnumerateDirectories(f.Folder,".sarah-recovery-*"));
    }

    [Fact]
    public async Task CancellationAndTimeoutStopTheWorkerWithoutPublishingOrLeavingItsStage()
    {
        using var f=new TestStore();var cipher=new RecoveryCipher();var material=await cipher.CreateAsync(BusinessFixture.Phrase);string source=Path.Combine(f.Folder,"fictional-source.bin"),encrypted=Path.Combine(f.Folder,"fictional.age"),restored=Path.Combine(f.Folder,"must-not-publish.bin");
        await File.WriteAllBytesAsync(source,new byte[150000]);await cipher.EncryptAsync(source,encrypted,material.Recipient);
        using var cancellation=new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>cipher.DecryptAsync(encrypted,restored,material.ProtectedIdentity,BusinessFixture.Phrase,cancellation.Token));
        Assert.False(File.Exists(restored));Assert.Empty(Directory.EnumerateDirectories(f.Folder,".sarah-recovery-*"));
        var limited=new RecoveryCipher(Path.Combine(AppContext.BaseDirectory,"SarahBeauty.Recovery.exe"),TimeSpan.FromMilliseconds(1));
        await Assert.ThrowsAsync<TimeoutException>(()=>limited.DecryptAsync(encrypted,restored,material.ProtectedIdentity,BusinessFixture.Phrase));Assert.False(File.Exists(restored));Assert.Empty(Directory.EnumerateDirectories(f.Folder,".sarah-recovery-*"));
    }

    [Fact]
    public async Task AnOutputCreatedByAnotherOperationIsPreservedAtPublication()
    {
        using var f=new TestStore();string destination=Path.Combine(f.Folder,"existing.bin");byte[] staged=[1,2,3],existing=[8,9];
        using(var stage=new RecoveryOutputStage(destination))
        {
            await File.WriteAllBytesAsync(stage.Path,staged);await File.WriteAllBytesAsync(destination,existing);
            await Assert.ThrowsAsync<IOException>(()=>stage.Publish(new(){Ok=true,Bytes=3,Sha256=Convert.ToHexString(SHA256.HashData(staged))},CancellationToken.None));
        }
        Assert.Equal(existing,await File.ReadAllBytesAsync(destination));Assert.Empty(Directory.EnumerateDirectories(f.Folder,".sarah-recovery-*"));
    }

    [Fact]
    public async Task AWrongOutputHashIsRejectedBeforePublicationAndAMissingHelperHasAClearError()
    {
        using var f=new TestStore();string destination=Path.Combine(f.Folder,"must-not-publish.bin");
        using(var stage=new RecoveryOutputStage(destination))
        {
            await File.WriteAllBytesAsync(stage.Path,[1,2,3]);await Assert.ThrowsAsync<RecoveryFailure>(()=>stage.Publish(new(){Ok=true,Bytes=3,Sha256="BAD"},CancellationToken.None));Assert.False(File.Exists(destination));
        }
        var cipher=new RecoveryCipher(Path.Combine(f.Folder,"missing-helper.exe"),TimeSpan.FromSeconds(1));var failure=await Assert.ThrowsAsync<RecoveryFailure>(()=>cipher.CreateAsync(BusinessFixture.Phrase));Assert.Equal("HelperMissing",failure.Code);
        var oversized=await Assert.ThrowsAsync<RecoveryFailure>(()=>new RecoveryCipher().InspectAsync(new byte[65537],BusinessFixture.Phrase));Assert.Equal("InvalidRequest",oversized.Code);
        Assert.Empty(Directory.EnumerateDirectories(f.Folder,".sarah-recovery-*"));
    }
}
