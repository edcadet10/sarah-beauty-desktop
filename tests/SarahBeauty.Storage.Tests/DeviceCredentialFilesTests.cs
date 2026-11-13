using System.Security.Cryptography;
using System.Text;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class DeviceCredentialFilesTests
{
    private static byte[] Fictional(string value)=>Encoding.UTF8.GetBytes("Fictional TEST credential material: "+value);
    [Fact]
    public async Task ReviewedCredentialCannotBeReplacedUntilItsDecisionFinishesAndPlainBytesAreCleared()
    {
        using var f=new BusinessFixture();using var root=new TestStore();var files=new DeviceCredentialFiles(f.Store,root.Folder);
        byte[] original=Fictional("reviewed PIN verifier"),replacement=Fictional("replacement PIN verifier");files.Write(DeviceCredentialKind.OwnerPin,original);
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();using var started=new ManualResetEventSlim();byte[]? reviewed=null;
        var read=Task.Run(()=>files.Review(DeviceCredentialKind.OwnerPin,clear=>{reviewed=clear;entered.Set();if(!release.Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException();Assert.Equal(original,clear);return true;}));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        var write=Task.Run(()=>{started.Set();files.Write(DeviceCredentialKind.OwnerPin,replacement);});Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        await Task.Delay(150);bool blocked=!write.IsCompleted;release.Set();await Task.WhenAll(read,write).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(blocked);Assert.NotNull(reviewed);Assert.All(reviewed,b=>Assert.Equal(0,b));Assert.Equal(replacement,files.Read(DeviceCredentialKind.OwnerPin));
    }
    [Fact]
    public void ProtectedCredentialsReopenOutsideTheBusinessStoreAndNeverEnterAnArchive()
    {
        using var f=new LedgerFixture();using var privateFiles=new TestStore();var files=new DeviceCredentialFiles(f.Business.Store,privateFiles.Folder);byte[] token=Fictional("first token"),client=Fictional("desktop client");
        Assert.Null(files.Read(DeviceCredentialKind.GoogleToken));files.Write(DeviceCredentialKind.GoogleToken,token);files.Write(DeviceCredentialKind.GoogleClient,client);
        var reopened=new DeviceCredentialFiles(new BusinessStore(f.Business.Files.DatabasePath),privateFiles.Folder);Assert.Equal(token,reopened.Read(DeviceCredentialKind.GoogleToken));Assert.Equal(client,reopened.Read(DeviceCredentialKind.GoogleClient));
        byte[] encrypted=File.ReadAllBytes(Path.Combine(files.DirectoryPath,"GoogleToken.dat"));Assert.Equal(-1,encrypted.AsSpan().IndexOf(token));
        using var captured=new ArchiveSources(f.Business.Store).Capture(f.Business.Owner);Assert.Single(captured.Manifest.Files);Assert.Equal(-1,File.ReadAllBytes(captured.FilePath("sarahbeauty.db")).AsSpan().IndexOf(token));
        files.Delete(DeviceCredentialKind.GoogleToken);Assert.Null(files.Read(DeviceCredentialKind.GoogleToken));Assert.Equal(client,files.Read(DeviceCredentialKind.GoogleClient));
    }

    [Fact]
    public void ChangedInstanceCannotUseAnEarlierFileOrStaleCredentialHandle()
    {
        using var f=new LedgerFixture();using var privateFiles=new TestStore();var files=new DeviceCredentialFiles(f.Business.Store,privateFiles.Folder);files.Write(DeviceCredentialKind.GoogleToken,Fictional("original instance"));
        byte[] original=File.ReadAllBytes(Path.Combine(files.DirectoryPath,"GoogleToken.dat"));f.Business.Files.Execute($"UPDATE AppMetadata SET Value='{Guid.NewGuid():N}' WHERE Key='WritableInstanceId';");
        Assert.Throws<UnauthorizedAccessException>(()=>files.Read(DeviceCredentialKind.GoogleToken));Assert.Throws<UnauthorizedAccessException>(()=>files.Write(DeviceCredentialKind.GoogleToken,Fictional("stale write")));
        var current=new DeviceCredentialFiles(new BusinessStore(f.Business.Files.DatabasePath),privateFiles.Folder);Assert.Null(current.Read(DeviceCredentialKind.GoogleToken));Directory.CreateDirectory(current.DirectoryPath);File.WriteAllBytes(Path.Combine(current.DirectoryPath,"GoogleToken.dat"),original);
        var error=Assert.Throws<InvalidDataException>(()=>current.Read(DeviceCredentialKind.GoogleToken));Assert.Contains("reconnect Google",error.Message);Assert.Null(error.InnerException);Assert.Equal(original,File.ReadAllBytes(Path.Combine(current.DirectoryPath,"GoogleToken.dat")));
    }

    [Fact]
    public void PurposeAndBusinessBindingRejectCopiedOrChangedCiphertextWithoutOverwritingIt()
    {
        using var f=new LedgerFixture();using var other=new BusinessFixture();using var privateFiles=new TestStore();var files=new DeviceCredentialFiles(f.Business.Store,privateFiles.Folder);files.Write(DeviceCredentialKind.GoogleToken,Fictional("private purpose"));
        byte[] encrypted=File.ReadAllBytes(Path.Combine(files.DirectoryPath,"GoogleToken.dat"));File.WriteAllBytes(Path.Combine(files.DirectoryPath,"GoogleClient.dat"),encrypted);Assert.Throws<InvalidDataException>(()=>files.Read(DeviceCredentialKind.GoogleClient));
        var foreign=new DeviceCredentialFiles(other.Store,privateFiles.Folder);Directory.CreateDirectory(foreign.DirectoryPath);File.WriteAllBytes(Path.Combine(foreign.DirectoryPath,"GoogleToken.dat"),encrypted);Assert.Throws<InvalidDataException>(()=>foreign.Read(DeviceCredentialKind.GoogleToken));
        encrypted[^1]^=1;File.WriteAllBytes(Path.Combine(files.DirectoryPath,"GoogleToken.dat"),encrypted);Assert.Throws<InvalidDataException>(()=>files.Read(DeviceCredentialKind.GoogleToken));Assert.Equal(encrypted,File.ReadAllBytes(Path.Combine(files.DirectoryPath,"GoogleToken.dat")));
    }

    [Fact]
    public void InterruptedReplacementPreservesThePreviousProtectedFileAndClearsOnlyItsOwnStage()
    {
        using var f=new LedgerFixture();using var privateFiles=new TestStore();var files=new DeviceCredentialFiles(f.Business.Store,privateFiles.Folder);byte[] original=Fictional("retained credential");files.Write(DeviceCredentialKind.GoogleToken,original);
        string unexpected=Path.Combine(files.DirectoryPath,"unrelated.partial");File.WriteAllText(unexpected,"Fictional unrelated retained file");files.FailurePoint=_=>throw new IOException("Fictional interrupted replacement");
        Assert.Throws<IOException>(()=>files.Write(DeviceCredentialKind.GoogleToken,Fictional("must not replace")));Assert.Equal(original,files.Read(DeviceCredentialKind.GoogleToken));Assert.Equal([unexpected],Directory.GetFiles(files.DirectoryPath,"*.partial"));
        files.FailurePoint=null;files.Write(DeviceCredentialKind.GoogleToken,Fictional("valid replacement"));Assert.Equal(Fictional("valid replacement"),files.Read(DeviceCredentialKind.GoogleToken));
    }

    [Fact]
    public void ChangedInstanceDuringFlushCannotPublishAStaleCredentialReplacement()
    {
        using var f=new LedgerFixture();using var privateFiles=new TestStore();var files=new DeviceCredentialFiles(f.Business.Store,privateFiles.Folder);files.Write(DeviceCredentialKind.GoogleToken,Fictional("retained original"));
        string path=Path.Combine(files.DirectoryPath,"GoogleToken.dat");byte[] original=File.ReadAllBytes(path);files.FailurePoint=_=>f.Business.Files.Execute($"UPDATE AppMetadata SET Value='{Guid.NewGuid():N}' WHERE Key='WritableInstanceId';");
        Assert.Throws<UnauthorizedAccessException>(()=>files.Write(DeviceCredentialKind.GoogleToken,Fictional("stale replacement")));Assert.Equal(original,File.ReadAllBytes(path));Assert.Empty(Directory.GetFiles(files.DirectoryPath,"*.partial"));
    }

    [Fact]
    public void BoundsAndPrivateStorageSeparationFailBeforePublishingCredentials()
    {
        using var f=new LedgerFixture();using var privateFiles=new TestStore();Assert.Throws<ArgumentException>(()=>new DeviceCredentialFiles(f.Business.Store,f.Business.Files.Folder));
        Assert.Throws<ArgumentException>(()=>new DeviceCredentialFiles(f.Business.Store,Path.Combine(f.Business.Files.Folder,"Secrets")));
        Assert.Throws<ArgumentException>(()=>new DeviceCredentialFiles(f.Business.Store,Path.GetPathRoot(f.Business.Files.Folder)!));
        var files=new DeviceCredentialFiles(f.Business.Store,privateFiles.Folder);Assert.Throws<ArgumentException>(()=>files.Write(DeviceCredentialKind.GoogleToken,[]));Assert.Throws<ArgumentException>(()=>files.Write(DeviceCredentialKind.GoogleToken,new byte[DeviceCredentialProtection.MaximumPlainBytes+1]));
        Assert.Throws<ArgumentOutOfRangeException>(()=>files.Read((DeviceCredentialKind)99));Assert.False(Directory.Exists(files.DirectoryPath));
        Directory.CreateDirectory(files.DirectoryPath);File.WriteAllBytes(Path.Combine(files.DirectoryPath,"GoogleToken.dat"),new byte[DeviceCredentialProtection.MaximumProtectedBytes+1]);Assert.Throws<InvalidDataException>(()=>files.Read(DeviceCredentialKind.GoogleToken));
    }

    [Fact]
    public async Task ConcurrentReplacementsAreReadableAsWholeProtectedValues()
    {
        using var f=new LedgerFixture();using var privateFiles=new TestStore();var files=new DeviceCredentialFiles(f.Business.Store,privateFiles.Folder);files.Write(DeviceCredentialKind.GoogleToken,Fictional("zero"));
        await Task.WhenAll(Enumerable.Range(0,12).Select(i=>Task.Run(()=>
        {
            var handle=new DeviceCredentialFiles(new BusinessStore(f.Business.Files.DatabasePath),privateFiles.Folder);handle.Write(DeviceCredentialKind.GoogleToken,Fictional(i.ToString()));byte[]? read=handle.Read(DeviceCredentialKind.GoogleToken);Assert.NotNull(read);Assert.StartsWith("Fictional TEST credential material: ",Encoding.UTF8.GetString(read));CryptographicOperations.ZeroMemory(read);
        })));
        Assert.Empty(Directory.GetFiles(files.DirectoryPath,"*.partial"));
    }
    [Fact]
    public async Task AReaderWaitsForTheCredentialPublicationGateBeforeDecidingTheFileIsMissing()
    {
        using var f=new BusinessFixture();using var privateFiles=new TestStore();var writer=new DeviceCredentialFiles(f.Store,privateFiles.Folder);var reader=new DeviceCredentialFiles(f.Store,privateFiles.Folder);writer.Write(DeviceCredentialKind.GoogleToken,Fictional("retained"));string path=Path.Combine(writer.DirectoryPath,"GoogleToken.dat"),parked=Path.Combine(writer.DirectoryPath,"fictional-parked.dat");Task<byte[]?>? pending=null;using var started=new ManualResetEventSlim();
        writer.FailurePoint=phase=>
        {
            if(phase!="Protected file flushed")return;File.Move(path,parked);
            try
            {
                pending=Task.Factory.StartNew(()=>{started.Set();return reader.Read(DeviceCredentialKind.GoogleToken);},CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);Assert.True(started.Wait(TimeSpan.FromSeconds(5)));Assert.False(pending.Wait(TimeSpan.FromMilliseconds(250)),"A credential read must wait for the current publisher before reporting absence.");
            }
            finally{File.Move(parked,path);}
        };
        byte[] replacement=Fictional("replacement after gate");writer.Write(DeviceCredentialKind.GoogleToken,replacement);Assert.NotNull(pending);Assert.Equal(replacement,await pending.WaitAsync(TimeSpan.FromSeconds(5)));Assert.Empty(Directory.GetFiles(writer.DirectoryPath,"*.partial"));
    }
}
