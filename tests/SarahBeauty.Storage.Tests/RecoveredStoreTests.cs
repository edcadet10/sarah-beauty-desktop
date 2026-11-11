using System.IO.Compression;
using System.Text.Json;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class RecoveredStoreTests
{
    private const string NewPhrase="fictional orchard lantern recovery words";
    private static RestoreCopyDecision Decision(LedgerFixture f)=>new(f.Business.Owner.OwnerId,"Fictional replacement-device preparation",true);
    private static async Task<StagedArchiveReview> Review(LedgerFixture f)
    {
        f.Post(f.Draft(FinancialKind.Receipt,10000));f.Post(f.Draft(FinancialKind.Expense,2500));var invoice=ApplicationTests.Invoice(f,10000);new DocumentArtifacts(f.Business.Store).Render(f.Business.Owner,invoice.Id);
        new ManagedAttachments(f.Business.Store).Import(f.Business.Owner,ManagedAttachmentTests.Pdf(f.Business),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());
        var cipher=new RecoveryCipher();var key=await cipher.CreateAsync(BusinessFixture.Phrase);string archive=Path.Combine(f.Business.Files.Folder,"fictional-recovery.age");
        var created=await new FullArchives(f.Business.Store).CreateAsync(f.Business.Owner,archive,key.Recipient);return await new ArchiveRestores().StageAsync(archive,f.Business.Files.Folder,key.ProtectedIdentity,BusinessFixture.Phrase,created);
    }
    private static void NoPublication(TestStore target)
    {
        string stores=Path.Combine(target.Folder,"RestoredStores"),staging=Path.Combine(target.Folder,"RecoveryStaging");
        if(Directory.Exists(stores))Assert.Empty(Directory.EnumerateFileSystemEntries(stores));if(Directory.Exists(staging))Assert.Empty(Directory.EnumerateFileSystemEntries(staging));
    }

    [Fact]
    public async Task PreparedCopyRetainsBusinessHistoryAndFilesWithFreshInstanceAndExplicitRecoveredOwnerAccess()
    {
        using var f=new LedgerFixture();using var target=new TestStore();using var review=await Review(f);long sourceWatermark=review.Manifest.Watermark;
        string[] originals=f.Business.Store.Read(db=>db.Read("SELECT DataJson FROM Movements ORDER BY Id;",r=>r.GetString(0)).ToArray());
        var prepared=await new RecoveredStores().PrepareAsync(review,target.Folder,Decision(f),NewPhrase,Guid.NewGuid().ToString());var store=new BusinessStore(prepared.DatabasePath);var access=new OwnerAccess(store);
        Assert.Equal(f.Business.Store.StoreId,store.StoreId);Assert.NotEqual(f.Business.Store.InstanceId,store.InstanceId);Assert.Equal(prepared.Receipt.InstanceId,store.InstanceId);Assert.Throws<UnauthorizedAccessException>(()=>store.BusinessName(f.Business.Owner));
        Assert.Throws<UnauthorizedAccessException>(()=>access.SignIn(f.Business.Owner.OwnerId,BusinessFixture.Phrase));var owner=access.SignIn(f.Business.Owner.OwnerId,NewPhrase);Assert.Equal("Fictional Beauty",store.BusinessName(owner));
        var other=access.Owners().Single(p=>p.Id!=owner.OwnerId);Assert.Equal("Fictional Beauty",store.BusinessName(access.SignIn(other.Id,BusinessFixture.Phrase)));
        Assert.Equal(originals,store.Read(db=>db.Read("SELECT DataJson FROM Movements ORDER BY Id;",r=>r.GetString(0)).ToArray()));
        foreach(var file in review.Manifest.Files.Where(p=>p.Kind!="Database"))Assert.True(File.ReadAllBytes(review.FilePath(file.RelativePath)).SequenceEqual(File.ReadAllBytes(Path.Combine(prepared.Folder,file.RelativePath))));
        Assert.Equal(sourceWatermark+1,TestStore.Scalar(prepared.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Equal(sourceWatermark,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Equal(7500,f.Balance("1010"));
        string provenance=(string)TestStore.Scalar(prepared.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key GLOB 'RecoveredStore/*';")!;Assert.False(provenance.Contains(NewPhrase,StringComparison.Ordinal));Assert.False(provenance.Contains(BusinessFixture.Phrase,StringComparison.Ordinal));
        Assert.Equal(prepared.Receipt,JsonSerializer.Deserialize<RecoveredStoreReceipt>(provenance));Assert.Equal(review.ArchiveHash,prepared.Receipt.SourceArchiveHash);
        var book=new FinanceBook(store);var draft=book.SaveDraft(owner,f.Draft(FinancialKind.Expense,500));book.Approve(owner,draft.Id,draft.Revision,NewPhrase);
        Assert.Equal(7000,new LedgerReports(store).TrialBalance(owner,DateOnly.MinValue,DateOnly.MaxValue).Single(p=>p.Account.Code=="1010").Ending);Assert.Equal(7500,f.Balance("1010"));
        review.Dispose();Assert.True(File.Exists(prepared.DatabasePath));
    }

    [Fact]
    public async Task RepeatingAPublishedRequestPreservesTheExistingRecoveredCopy()
    {
        using var f=new LedgerFixture();using var target=new TestStore();using var review=await Review(f);var service=new RecoveredStores();string request=Guid.NewGuid().ToString();
        var prepared=await service.PrepareAsync(review,target.Folder,Decision(f),NewPhrase,request);byte[] before=File.ReadAllBytes(prepared.DatabasePath);
        await Assert.ThrowsAsync<IOException>(()=>service.PrepareAsync(review,target.Folder,Decision(f),NewPhrase,request));Assert.True(before.SequenceEqual(File.ReadAllBytes(prepared.DatabasePath)));Assert.Single(Directory.EnumerateDirectories(Path.Combine(target.Folder,"RestoredStores")));
    }

    [Fact]
    public async Task ChangedStagedBytesCannotPublishARecoveredCopy()
    {
        using var f=new LedgerFixture();using var target=new TestStore();using var review=await Review(f);string path=review.FilePath(review.Manifest.Files.Single(p=>p.Kind=="Attachment").RelativePath);File.AppendAllText(path,"Fictional altered staged file");
        await Assert.ThrowsAsync<InvalidDataException>(()=>new RecoveredStores().PrepareAsync(review,target.Folder,Decision(f),NewPhrase,Guid.NewGuid().ToString()));NoPublication(target);Assert.Equal(7500,f.Balance("1010"));
    }

    [Fact]
    public async Task MutableDisplayFactsDoNotReplaceTheOriginalHashedManifest()
    {
        using var f=new LedgerFixture();using var target=new TestStore();using var review=await Review(f);review.Manifest.Files[0]=review.Manifest.Files[0] with{Sha256=new string('0',64)};review.Manifest.Facts.Tables[0]=review.Manifest.Facts.Tables[0] with{Rows=999999};
        var prepared=await new RecoveredStores().PrepareAsync(review,target.Folder,Decision(f),NewPhrase,Guid.NewGuid().ToString());Assert.Equal(2L,TestStore.Scalar(prepared.DatabasePath,"SELECT COUNT(*) FROM Movements;"));Assert.Equal(7500,f.Balance("1010"));
    }

    [Fact]
    public async Task ChangedManifestOrDisposedReviewCannotAuthorizePreparation()
    {
        using var f=new LedgerFixture();using var target=new TestStore();using var review=await Review(f);
        string package=Path.Combine(Path.GetDirectoryName(review.FilePath("sarahbeauty.db"))!,"payload.zip");
        using(var zip=ZipFile.Open(package,ZipArchiveMode.Update))
        {
            zip.GetEntry("manifest.json")!.Delete();using var output=zip.CreateEntry("manifest.json").Open();output.Write(JsonSerializer.SerializeToUtf8Bytes(review.Manifest with{BusinessName="Fictional changed manifest"}));
        }
        await Assert.ThrowsAsync<InvalidDataException>(()=>new RecoveredStores().PrepareAsync(review,target.Folder,Decision(f),NewPhrase,Guid.NewGuid().ToString()));NoPublication(target);review.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(()=>new RecoveredStores().PrepareAsync(review,target.Folder,Decision(f),NewPhrase,Guid.NewGuid().ToString()));NoPublication(target);
    }

    [Theory]
    [InlineData("acknowledgment")]
    [InlineData("reason")]
    [InlineData("owner")]
    [InlineData("passphrase")]
    public async Task IncompleteRecoveryDecisionsCannotCreateAWritableCopy(string defect)
    {
        using var f=new LedgerFixture();using var target=new TestStore();using var review=await Review(f);var decision=Decision(f);string phrase=NewPhrase;
        if(defect=="acknowledgment")decision=decision with{PreviousPrimaryAcknowledged=false};if(defect=="reason")decision=decision with{Reason=""};if(defect=="owner")decision=decision with{OwnerId=Guid.NewGuid().ToString()};if(defect=="passphrase")phrase="short";
        await Assert.ThrowsAsync<ArgumentException>(()=>new RecoveredStores().PrepareAsync(review,target.Folder,decision,phrase,Guid.NewGuid().ToString()));NoPublication(target);Assert.Equal(7500,f.Balance("1010"));
    }

    [Theory]
    [InlineData("Recovered files copied",false)]
    [InlineData("Recovered access prepared",false)]
    [InlineData("Before recovered copy published",false)]
    [InlineData("Recovered files copied",true)]
    [InlineData("Recovered access prepared",true)]
    [InlineData("Before recovered copy published",true)]
    public async Task InterruptedPreparationPreservesTheOriginalAndTheValidatedReview(string stage,bool cancel)
    {
        using var f=new LedgerFixture();using var target=new TestStore();using var review=await Review(f);using var cancellation=new CancellationTokenSource();
        var service=new RecoveredStores{FailurePoint=point=>{if(point!=stage)return;if(cancel)cancellation.Cancel();else throw new IOException("Fictional interrupted recovered-copy preparation");}};
        if(cancel)await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.PrepareAsync(review,target.Folder,Decision(f),NewPhrase,Guid.NewGuid().ToString(),cancellation.Token));
        else await Assert.ThrowsAsync<IOException>(()=>service.PrepareAsync(review,target.Folder,Decision(f),NewPhrase,Guid.NewGuid().ToString()));
        NoPublication(target);Assert.True(File.Exists(review.FilePath("sarahbeauty.db")));Assert.Equal(7500,f.Balance("1010"));Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
    }

    [Fact]
    public async Task ReviewClosedImmediatelyBeforePublicationPreventsTheNewCopy()
    {
        using var f=new LedgerFixture();using var target=new TestStore();using var review=await Review(f);
        var service=new RecoveredStores{FailurePoint=point=>{if(point=="Before recovered copy published")review.Dispose();}};
        await Assert.ThrowsAsync<ObjectDisposedException>(()=>service.PrepareAsync(review,target.Folder,Decision(f),NewPhrase,Guid.NewGuid().ToString()));NoPublication(target);Assert.Equal(7500,f.Balance("1010"));
    }

    [Fact]
    public async Task ANewlyCreatedDestinationIsPreservedAtTheFinalPublication()
    {
        using var f=new LedgerFixture();using var target=new TestStore();using var review=await Review(f);string request=Guid.NewGuid().ToString(),destination=Path.Combine(target.Folder,"RestoredStores",Guid.Parse(request).ToString("N"));
        var service=new RecoveredStores{FailurePoint=point=>{if(point=="Before recovered copy published")File.WriteAllText(destination,"Fictional concurrent destination");}};
        await Assert.ThrowsAsync<IOException>(()=>service.PrepareAsync(review,target.Folder,Decision(f),NewPhrase,request));Assert.Equal("Fictional concurrent destination",File.ReadAllText(destination));Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(target.Folder,"RecoveryStaging")));Assert.Equal(7500,f.Balance("1010"));
    }

    [Fact]
    public async Task EarlierArchivesPrepareWithCurrentSchemaWithoutChangingTheirReviewedDatabase()
    {
        using var f=new BusinessFixture();using var legacy=new TestStore();using var target=new TestStore();var older=await StoreInstanceTests.EarlierArchive(f,legacy);
        using var review=await new ArchiveRestores().StageAsync(older.Archive,legacy.Folder,older.Key.ProtectedIdentity,BusinessFixture.Phrase);string before=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(review.FilePath("sarahbeauty.db"))));
        var prepared=await new RecoveredStores().PrepareAsync(review,target.Folder,new(f.Owner.OwnerId,"Fictional older archive recovery",true),NewPhrase,Guid.NewGuid().ToString());
        Assert.Equal(32L,TestStore.Scalar(prepared.DatabasePath,"PRAGMA user_version;"));Assert.Equal(31L,TestStore.Scalar(review.FilePath("sarahbeauty.db"),"PRAGMA user_version;"));Assert.Equal(before,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(review.FilePath("sarahbeauty.db")))));Assert.Null(prepared.Receipt.SourceInstanceId);
        var store=new BusinessStore(prepared.DatabasePath);Assert.Equal("Fictional Beauty",store.BusinessName(new OwnerAccess(store).SignIn(f.Owner.OwnerId,NewPhrase)));Assert.Equal("Fictional Beauty",f.Store.BusinessName(f.Owner));
    }
}
