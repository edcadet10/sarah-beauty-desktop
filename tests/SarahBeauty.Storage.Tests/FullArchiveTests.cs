using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Recovery;

namespace SarahBeauty.Storage.Tests;

public sealed class FullArchiveTests
{
    private static string Output(LedgerFixture f)=>Path.Combine(f.Business.Files.Folder,"fictional-complete.age");
    private static void Clean(LedgerFixture f)
    {
        Assert.Empty(Directory.EnumerateDirectories(f.Business.Files.Folder,".sarah-recovery-*"));
        string staging=Path.Combine(f.Business.Files.Folder,"RecoveryStaging");if(Directory.Exists(staging))Assert.Empty(Directory.EnumerateFileSystemEntries(staging));
    }
    private static async Task<byte[]> Contents(ZipArchiveEntry entry)
    {
        using var source=entry.Open();using var output=new MemoryStream();await source.CopyToAsync(output);return output.ToArray();
    }

    [Fact]
    public async Task CompleteEncryptedArchiveRetainsFrozenDatabasePdfsAndEveryEvidenceVersion()
    {
        using var f=new LedgerFixture();f.Post(f.Draft(FinancialKind.Receipt,10000));f.Post(f.Draft(FinancialKind.Expense,2500));
        var invoice=ApplicationTests.Invoice(f,10000);new DocumentArtifacts(f.Business.Store).Render(f.Business.Owner,invoice.Id);
        var files=new ManagedAttachments(f.Business.Store);var first=files.Import(f.Business.Owner,ManagedAttachmentTests.Pdf(f.Business),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());
        files.Import(f.Business.Owner,ManagedAttachmentTests.Pdf(f.Business,"Fictional replacement.pdf"),ManagedAttachmentTests.Draft with{AttachmentId=first.Version.AttachmentId,ExpectedVersion=1},Guid.NewGuid().ToString());
        long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        var cipher=new RecoveryCipher();var material=await cipher.CreateAsync(BusinessFixture.Phrase);string encrypted=Output(f),plain=Path.Combine(f.Business.Files.Folder,"fictional-review.zip");
        var created=await new FullArchives(f.Business.Store).CreateAsync(f.Business.Owner,encrypted,material.Recipient);
        Assert.Equal(new FileInfo(encrypted).Length,created.ByteLength);Assert.Equal(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(encrypted))),created.Sha256);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material.Recipient))),created.RecipientFingerprint);
        await cipher.DecryptAsync(encrypted,plain,material.ProtectedIdentity,BusinessFixture.Phrase);
        using(var zip=ZipFile.OpenRead(plain))
        {
            byte[] raw=await Contents(zip.GetEntry("manifest.json")!);Assert.Equal(created.ManifestHash,Convert.ToHexString(SHA256.HashData(raw)));
            var manifest=JsonSerializer.Deserialize<ArchiveManifest>(raw)!;Assert.Equal(watermark,manifest.Watermark);Assert.Equal(4,manifest.Files.Length);Assert.Equal(manifest.Files.Length+1,zip.Entries.Count);
            Assert.Equal(2,manifest.Facts.MovementCount);Assert.Equal("12500",manifest.Facts.TotalDebits);Assert.Equal("12500",manifest.Facts.TotalCredits);
            foreach(var file in manifest.Files){byte[] bytes=await Contents(zip.GetEntry(file.RelativePath)!);Assert.Equal(file.ByteLength,bytes.LongLength);Assert.Equal(file.Sha256,Convert.ToHexString(SHA256.HashData(bytes)));}
            string database=Path.Combine(f.Business.Files.Folder,"fictional-reviewed.db");await File.WriteAllBytesAsync(database,await Contents(zip.GetEntry("sarahbeauty.db")!));
            Assert.Equal(2L,TestStore.Scalar(database,"SELECT COUNT(*) FROM Movements;"));Assert.Equal(watermark,TestStore.Scalar(database,"SELECT Watermark FROM Business;"));
        }
        Assert.Equal(watermark,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Equal(7500,f.Balance("1010"));Clean(f);
    }

    [Fact]
    public async Task ChangesAfterSnapshotRemainOutsideTheCompletedArchive()
    {
        using var f=new LedgerFixture();f.Post(f.Draft(FinancialKind.Receipt,10000));long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        var material=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);
        var archives=new FullArchives(f.Business.Store){FailurePoint=point=>{if(point=="Database captured")f.Post(f.Draft(FinancialKind.Expense,2500));}};
        var created=await archives.CreateAsync(f.Business.Owner,Output(f),material.Recipient);
        Assert.Equal(watermark,created.Manifest.Watermark);Assert.Equal(1,created.Manifest.Facts.MovementCount);Assert.Equal("10000",created.Manifest.Facts.TotalDebits);
        Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);Assert.True((long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!>watermark);Clean(f);
    }

    [Theory]
    [InlineData("Database captured",false)]
    [InlineData("File copied",false)]
    [InlineData("Package created",false)]
    [InlineData("Package encrypted",false)]
    [InlineData("Package created",true)]
    [InlineData("Package encrypted",true)]
    public async Task InterruptedArchivesDoNotPublishAndPreserveTheSource(string stage,bool cancel)
    {
        using var f=new LedgerFixture();var files=new ManagedAttachments(f.Business.Store);var imported=files.Import(f.Business.Owner,ManagedAttachmentTests.Pdf(f.Business),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());
        var material=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);using var cancellation=new CancellationTokenSource();
        var archives=new FullArchives(f.Business.Store){FailurePoint=point=>{if(point!=stage)return;if(cancel)cancellation.Cancel();else throw new IOException("Fictional interrupted archive");}};
        if(cancel)await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>archives.CreateAsync(f.Business.Owner,Output(f),material.Recipient,cancellation.Token));
        else await Assert.ThrowsAsync<IOException>(()=>archives.CreateAsync(f.Business.Owner,Output(f),material.Recipient));
        Assert.False(File.Exists(Output(f)));using var source=files.OpenRead(f.Business.Owner,imported.Version.Id);Assert.Equal(imported.Version.ByteLength,source.Length);Clean(f);
    }

    [Fact]
    public async Task AlteredPreparedPackageIsRejectedBeforeEncryption()
    {
        using var f=new LedgerFixture();var material=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);
        var archives=new FullArchives(f.Business.Store){FailurePoint=point=>{if(point=="Package created")File.AppendAllText(Directory.GetFiles(Path.Combine(f.Business.Files.Folder,"RecoveryStaging"),"payload.zip",SearchOption.AllDirectories).Single(),"Fictional changed package");}};
        await Assert.ThrowsAsync<InvalidDataException>(()=>archives.CreateAsync(f.Business.Owner,Output(f),material.Recipient));Assert.False(File.Exists(Output(f)));Clean(f);
    }

    [Fact]
    public void AlteredCapturedBytesCannotBePackagedWithTheOriginalManifest()
    {
        using var f=new LedgerFixture();using(var captured=new ArchiveSources(f.Business.Store).Capture(f.Business.Owner))
        {
            File.AppendAllText(captured.FilePath("sarahbeauty.db"),"Fictional changed captured bytes");Assert.Throws<InvalidDataException>(()=>ArchivePackage.Write(captured,CancellationToken.None));
        }
        Clean(f);
    }

    [Fact]
    public async Task ForeignSessionsInvalidRecipientsAndExistingDestinationsDoNotCaptureOrOverwrite()
    {
        using var f=new LedgerFixture();using var foreign=new BusinessFixture();bool captured=false;var archives=new FullArchives(f.Business.Store){FailurePoint=_=>captured=true};
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>archives.CreateAsync(foreign.Owner,Output(f),"invalid"));
        var invalid=await Assert.ThrowsAsync<RecoveryFailure>(()=>archives.CreateAsync(f.Business.Owner,Output(f),"invalid"));Assert.Equal("UnsupportedRecipient",invalid.Code);
        var material=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);await File.WriteAllTextAsync(Output(f),"Fictional existing archive");
        await Assert.ThrowsAsync<IOException>(()=>archives.CreateAsync(f.Business.Owner,Output(f),material.Recipient));Assert.False(captured);Assert.Equal("Fictional existing archive",await File.ReadAllTextAsync(Output(f)));Clean(f);
    }
}
