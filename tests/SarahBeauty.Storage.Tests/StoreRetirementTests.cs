using System.Text.Json;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class StoreRetirementTests
{
    private static async Task<LocalBackupJob> Backup(LedgerFixture f)
    {
        f.Post(f.Draft(FinancialKind.Receipt,10000));var key=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);await new RecoveryProfiles(f.Business.Store).RegisterAsync(f.Business.Owner,key.ProtectedIdentity,BusinessFixture.Phrase);
        return await new LocalBackups(f.Business.Store).CreateAsync(f.Business.Owner,Path.Combine(f.Business.Files.Folder,"fictional-current.age"));
    }
    private static void Active(LedgerFixture f)
    {
        Assert.Equal("Active",TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='WritableInstanceState';"));Assert.Equal(10000,f.Balance("1010"));Assert.Null(TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key GLOB 'RetiredStore/*';"));
    }

    [Fact]
    public async Task CurrentVerifiedArchiveAndOwnerProofRetireOnlyThisInstanceWithPreservedHistory()
    {
        using var f=new LedgerFixture();var job=await Backup(f);Assert.Equal(f.Business.Store.InstanceId,job.InstanceId);byte[] archive=File.ReadAllBytes(job.Destination);long watermark=job.Result!.Manifest.Watermark;
        var receipt=await new StoreRetirement(f.Business.Store).RetireAsync(f.Business.Owner,job.Id,"Fictional replacement of this local instance",BusinessFixture.Phrase);
        Assert.Equal(watermark,receipt.ArchivedWatermark);Assert.Equal(watermark+1,receipt.RetirementWatermark);Assert.Equal(f.Business.Store.InstanceId,receipt.InstanceId);Assert.Equal(job.Result.Sha256,receipt.ArchiveHash);
        Assert.Equal("Retired",TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='WritableInstanceState';"));Assert.Equal(1L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Movements;"));Assert.True(archive.SequenceEqual(File.ReadAllBytes(job.Destination)));
        Assert.Throws<UnauthorizedAccessException>(()=>f.Book.Posted(f.Business.Owner));Assert.Throws<UnauthorizedAccessException>(()=>new BusinessStore(f.Business.Files.DatabasePath));
        string json=(string)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key GLOB 'RetiredStore/*';")!;Assert.Equal(receipt,JsonSerializer.Deserialize<StoreRetirementReceipt>(json));Assert.False(json.Contains(BusinessFixture.Phrase,StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("changed")]
    [InlineData("older-coverage")]
    [InlineData("unknown-instance")]
    [InlineData("different-instance")]
    public async Task MissingChangedOldOrUnconfirmedInstanceArchivesCannotRetireTheStore(string defect)
    {
        using var f=new LedgerFixture();var job=await Backup(f);
        if(defect=="missing")File.Delete(job.Destination);if(defect=="changed")File.AppendAllText(job.Destination,"Fictional changed ciphertext");
        if(defect=="older-coverage")f.Post(f.Draft(FinancialKind.Expense,1000));
        if(defect is "unknown-instance" or "different-instance")f.Business.Store.Transaction(db=>db.Execute("UPDATE AppMetadata SET Value=$json WHERE Key=$key;",("$json",JsonSerializer.Serialize(job with{InstanceId=defect=="unknown-instance"?null:Guid.NewGuid().ToString("N")})),("$key",BackupJobRecords.Key(job.Id))));
        var service=new StoreRetirement(f.Business.Store);
        if(defect=="missing")await Assert.ThrowsAsync<FileNotFoundException>(()=>service.RetireAsync(f.Business.Owner,job.Id,"Fictional retirement review",BusinessFixture.Phrase));
        else if(defect=="changed")await Assert.ThrowsAsync<InvalidDataException>(()=>service.RetireAsync(f.Business.Owner,job.Id,"Fictional retirement review",BusinessFixture.Phrase));
        else await Assert.ThrowsAsync<ArgumentException>(()=>service.RetireAsync(f.Business.Owner,job.Id,"Fictional retirement review",BusinessFixture.Phrase));
        Assert.Equal("Active",TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='WritableInstanceState';"));Assert.Equal(defect=="older-coverage"?9000:10000,f.Balance("1010"));
    }

    [Fact]
    public async Task WrongProofForeignOwnerAndMissingReasonDoNotRetireTheStore()
    {
        using var f=new LedgerFixture();using var foreign=new BusinessFixture();var job=await Backup(f);var service=new StoreRetirement(f.Business.Store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.RetireAsync(f.Business.Owner,job.Id,"Fictional retirement review","fictional alternate incorrect words"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.RetireAsync(foreign.Owner,job.Id,"Fictional retirement review",BusinessFixture.Phrase));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.RetireAsync(f.Business.Owner,job.Id,"",BusinessFixture.Phrase));Active(f);
    }

    [Fact]
    public async Task LaterBusinessChangesAfterArchiveVerificationBlockRetirement()
    {
        using var f=new LedgerFixture();var job=await Backup(f);var service=new StoreRetirement(f.Business.Store){FailurePoint=point=>{if(point=="Archive verified")f.Post(f.Draft(FinancialKind.Expense,1000));}};
        await Assert.ThrowsAsync<ArgumentException>(()=>service.RetireAsync(f.Business.Owner,job.Id,"Fictional retirement review",BusinessFixture.Phrase));Assert.Equal("Active",TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='WritableInstanceState';"));Assert.Equal(9000,f.Balance("1010"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedRetirementRollsBackItsMetadataAuditAndWatermark(bool cancel)
    {
        using var f=new LedgerFixture();var job=await Backup(f);long watermark=job.Result!.Manifest.Watermark;using var cancellation=new CancellationTokenSource();
        var service=new StoreRetirement(f.Business.Store){FailurePoint=point=>{if(point!="Retirement recorded")return;if(cancel)cancellation.Cancel();else throw new IOException("Fictional retirement interruption");}};
        if(cancel)await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.RetireAsync(f.Business.Owner,job.Id,"Fictional retirement review",BusinessFixture.Phrase,cancellation.Token));
        else await Assert.ThrowsAsync<IOException>(()=>service.RetireAsync(f.Business.Owner,job.Id,"Fictional retirement review",BusinessFixture.Phrase));
        Assert.Equal(watermark,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;"));Active(f);Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Audit WHERE Action='Retired local writable store instance';"));
    }
}
