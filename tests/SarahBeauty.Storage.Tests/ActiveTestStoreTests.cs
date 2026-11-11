using System.Text.Json;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class ActiveTestStoreTests
{
    private const string NewPhrase="fictional orchard lantern recovery words";
    private static async Task<(LocalBackupJob Job,PreparedRecoveredStore Prepared)> Prepare(LedgerFixture f,string parent)
    {
        f.Post(f.Draft(FinancialKind.Receipt,10000));f.Post(f.Draft(FinancialKind.Expense,2500));new ManagedAttachments(f.Business.Store).Import(f.Business.Owner,ManagedAttachmentTests.Pdf(f.Business),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());
        var key=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);await new RecoveryProfiles(f.Business.Store).RegisterAsync(f.Business.Owner,key.ProtectedIdentity,BusinessFixture.Phrase);
        var job=await new LocalBackups(f.Business.Store).CreateAsync(f.Business.Owner,Path.Combine(f.Business.Files.Folder,"fictional-latest.age"));
        using var review=await new ArchiveRestores().StageAsync(job.Destination,parent,key.ProtectedIdentity,BusinessFixture.Phrase,job.Result);
        var prepared=await new RecoveredStores().PrepareAsync(review,parent,new(f.Business.Owner.OwnerId,"Fictional controlled activation",true),NewPhrase,Guid.NewGuid().ToString());return(job,prepared);
    }
    private static void Prepared(PreparedRecoveredStore prepared)
    {
        Assert.Equal("Prepared",TestStore.Scalar(prepared.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='WritableInstanceState';"));Assert.Throws<UnauthorizedAccessException>(()=>new BusinessStore(prepared.DatabasePath));
    }

    [Fact]
    public async Task PreparedCopiesCannotBecomeOrdinaryStoresUntilTheLocalPrimaryIsRetired()
    {
        using var f=new LedgerFixture();string parent=f.Business.Files.Folder;var created=await Prepare(f,parent);var service=new ActiveTestStores(parent);Prepared(created.Prepared);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.ActivateAsync(created.Prepared.Receipt.RequestId,f.Business.Owner.OwnerId,NewPhrase));Assert.Equal(f.Business.Files.DatabasePath,service.ResolveDatabase());Assert.Equal(7500,f.Balance("1010"));
        await new StoreRetirement(f.Business.Store).RetireAsync(f.Business.Owner,created.Job.Id,"Fictional local primary replacement",BusinessFixture.Phrase);
        var active=await service.ActivateAsync(created.Prepared.Receipt.RequestId,f.Business.Owner.OwnerId,NewPhrase);Assert.Equal(created.Prepared.DatabasePath,service.ResolveDatabase());Assert.Equal(created.Prepared.DatabasePath,new ActiveTestStores(parent).ResolveDatabase());
        Assert.Equal(created.Prepared.Receipt.InstanceId,active.Store.InstanceId);Assert.Equal("Active",TestStore.Scalar(active.Store.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='WritableInstanceState';"));Assert.Equal(created.Prepared.Receipt.SourceWatermark+2,TestStore.Scalar(active.Store.DatabasePath,"SELECT Watermark FROM Business;"));
        Assert.Throws<UnauthorizedAccessException>(()=>new BusinessStore(f.Business.Files.DatabasePath));Assert.Equal(2L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Movements;"));
        var book=new FinanceBook(active.Store);var draft=book.SaveDraft(active.Owner,f.Draft(FinancialKind.Expense,500));book.Approve(active.Owner,draft.Id,draft.Revision,NewPhrase);
        Assert.Equal(7000,new LedgerReports(active.Store).TrialBalance(active.Owner,DateOnly.MinValue,DateOnly.MaxValue).Single(p=>p.Account.Code=="1010").Ending);
        var repeated=await service.ActivateAsync(created.Prepared.Receipt.RequestId,f.Business.Owner.OwnerId,NewPhrase);Assert.Equal(active.Store.InstanceId,repeated.Store.InstanceId);Assert.Equal(3,new FinanceBook(repeated.Store).Posted(repeated.Owner).Count);
        string pointer=File.ReadAllText(Path.Combine(parent,"active-test-store.json"));Assert.False(pointer.Contains(NewPhrase,StringComparison.Ordinal));Assert.False(pointer.Contains(BusinessFixture.Phrase,StringComparison.Ordinal));
    }

    [Fact]
    public async Task RecoveryOnANewRootNeedsFreshOwnerProofAndPreservesTheOriginalSource()
    {
        using var f=new LedgerFixture();using var target=new TestStore();var created=await Prepare(f,target.Folder);var service=new ActiveTestStores(target.Folder);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.ActivateAsync(created.Prepared.Receipt.RequestId,f.Business.Owner.OwnerId,"fictional incorrect recovery words"));Prepared(created.Prepared);Assert.False(File.Exists(Path.Combine(target.Folder,"active-test-store.json")));
        var active=await service.ActivateAsync(created.Prepared.Receipt.RequestId,f.Business.Owner.OwnerId,NewPhrase);Assert.Equal(created.Prepared.DatabasePath,service.ResolveDatabase());Assert.Equal(7500,f.Balance("1010"));Assert.Equal(2,new FinanceBook(active.Store).Posted(active.Owner).Count);
    }

    [Theory]
    [InlineData("file")]
    [InlineData("watermark")]
    [InlineData("journal")]
    [InlineData("extra-record")]
    [InlineData("older-provenance")]
    public async Task AlteredPreparedCopiesNeverReplaceTheActiveSelection(string defect)
    {
        using var f=new LedgerFixture();using var target=new TestStore();var created=await Prepare(f,target.Folder);var prepared=created.Prepared;
        if(defect=="file")File.AppendAllText(Path.Combine(prepared.Folder,prepared.Receipt.SourceManifest!.Files.Single(p=>p.Kind=="Attachment").RelativePath),"Fictional changed bytes");
        else
        {
            var copy=new BusinessStore(prepared.DatabasePath,"Prepared");copy.Transaction(db=>defect switch
            {
                "watermark"=>db.Execute("UPDATE Business SET Watermark=Watermark+1;"),
                "journal"=>db.Execute("UPDATE JournalLines SET DebitCents=DebitCents+1 WHERE DebitCents>0;"),
                "extra-record"=>db.Execute("INSERT INTO Customers SELECT $id,BusinessId,SearchName,Archived,Revision,DataJson,CreatedAtUtc,CreatedBy,UpdatedAtUtc,UpdatedBy FROM Customers LIMIT 1;",("$id",Guid.NewGuid().ToString())),
                _=>db.Execute("UPDATE AppMetadata SET Value=$json WHERE Key=$key;",("$json",JsonSerializer.Serialize(prepared.Receipt with{Version=1,SourceManifest=null})),("$key","RecoveredStore/"+prepared.Receipt.RequestId))
            });
        }
        await Assert.ThrowsAsync<InvalidDataException>(()=>new ActiveTestStores(target.Folder).ActivateAsync(prepared.Receipt.RequestId,f.Business.Owner.OwnerId,NewPhrase));Prepared(prepared);Assert.False(File.Exists(Path.Combine(target.Folder,"active-test-store.json")));Assert.Equal(7500,f.Balance("1010"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureAfterPointerPublicationRollsBackSelectionStateAndAudit(bool cancel)
    {
        using var f=new LedgerFixture();using var target=new TestStore();var created=await Prepare(f,target.Folder);using var cancellation=new CancellationTokenSource();
        var service=new ActiveTestStores(target.Folder){FailurePoint=point=>{if(point!="Selection published")return;if(cancel)cancellation.Cancel();else throw new IOException("Fictional activation interruption");}};
        if(cancel)await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.ActivateAsync(created.Prepared.Receipt.RequestId,f.Business.Owner.OwnerId,NewPhrase,cancellation.Token));
        else await Assert.ThrowsAsync<IOException>(()=>service.ActivateAsync(created.Prepared.Receipt.RequestId,f.Business.Owner.OwnerId,NewPhrase));
        Prepared(created.Prepared);Assert.False(File.Exists(Path.Combine(target.Folder,"active-test-store.json")));Assert.Equal(0L,TestStore.Scalar(created.Prepared.DatabasePath,"SELECT COUNT(*) FROM Audit WHERE Action='Activated recovered TEST working store';"));
    }

    [Fact]
    public async Task ReplacingAnExistingSelectionPreservesItsPriorPointerOnFailure()
    {
        using var first=new LedgerFixture();using var second=new LedgerFixture();using var target=new TestStore();var initial=await Prepare(first,target.Folder);var service=new ActiveTestStores(target.Folder);
        await service.ActivateAsync(initial.Prepared.Receipt.RequestId,first.Business.Owner.OwnerId,NewPhrase);byte[] before=File.ReadAllBytes(Path.Combine(target.Folder,"active-test-store.json"));var next=await Prepare(second,target.Folder);
        service.FailurePoint=point=>{if(point=="Selection published")throw new IOException("Fictional interrupted replacement");};
        await Assert.ThrowsAsync<IOException>(()=>service.ActivateAsync(next.Prepared.Receipt.RequestId,second.Business.Owner.OwnerId,NewPhrase));Assert.True(before.SequenceEqual(File.ReadAllBytes(Path.Combine(target.Folder,"active-test-store.json"))));Assert.Equal(initial.Prepared.DatabasePath,service.ResolveDatabase());Prepared(next.Prepared);
        service.FailurePoint=null;await service.ActivateAsync(next.Prepared.Receipt.RequestId,second.Business.Owner.OwnerId,NewPhrase);Assert.Equal(next.Prepared.DatabasePath,service.ResolveDatabase());Assert.True(File.Exists(initial.Prepared.DatabasePath));
    }

    [Fact]
    public async Task ACommittedSelectionIsPreservedIfTheCallerFailsBeforeOpeningItsWorkspace()
    {
        using var f=new LedgerFixture();using var target=new TestStore();var created=await Prepare(f,target.Folder);
        var service=new ActiveTestStores(target.Folder){FailurePoint=point=>{if(point=="Selection committed")throw new IOException("Fictional post-commit interruption");}};
        var error=await Assert.ThrowsAsync<IOException>(()=>service.ActivateAsync(created.Prepared.Receipt.RequestId,f.Business.Owner.OwnerId,NewPhrase));Assert.Contains("was selected",error.Message);Assert.Equal(created.Prepared.DatabasePath,new ActiveTestStores(target.Folder).ResolveDatabase());Assert.Equal("Active",TestStore.Scalar(created.Prepared.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='WritableInstanceState';"));
    }

    [Theory]
    [InlineData("../outside.db")]
    [InlineData("C:/outside.db")]
    [InlineData("RestoredStores/00000000000000000000000000000001/../../outside.db")]
    public void UnsafeOrMalformedPointerDataNeverFallsBackToAnotherStore(string relative)
    {
        using var target=new TestStore();string pointer=Path.Combine(target.Folder,"active-test-store.json");var value=new ActiveTestSelection(1,Guid.NewGuid().ToString(),Guid.NewGuid().ToString(),Guid.NewGuid().ToString("N"),relative,DateTimeOffset.UtcNow.ToString("O"));
        File.WriteAllText(pointer,JsonSerializer.Serialize(value));byte[] before=File.ReadAllBytes(pointer);Assert.Throws<InvalidDataException>(()=>new ActiveTestStores(target.Folder).ResolveDatabase());Assert.True(before.SequenceEqual(File.ReadAllBytes(pointer)));
        File.WriteAllText(pointer,"not json");Assert.Throws<InvalidDataException>(()=>new ActiveTestStores(target.Folder).ResolveDatabase());Assert.Equal("not json",File.ReadAllText(pointer));
    }

    [Fact]
    public async Task AnInterruptedPointerToAStillPreparedCopyCanBeCompletedWithFreshProof()
    {
        using var f=new LedgerFixture();using var target=new TestStore();var created=await Prepare(f,target.Folder);var r=created.Prepared.Receipt;
        var selection=new ActiveTestSelection(1,r.RequestId,r.StoreId,r.InstanceId,"RestoredStores/"+Guid.Parse(r.RequestId).ToString("N")+"/sarahbeauty.db",DateTimeOffset.UtcNow.ToString("O"));
        File.WriteAllText(Path.Combine(target.Folder,"active-test-store.json"),JsonSerializer.Serialize(selection));var service=new ActiveTestStores(target.Folder);Assert.Equal(created.Prepared.DatabasePath,service.ResolveDatabase());Prepared(created.Prepared);
        var active=await service.ActivateAsync(r.RequestId,r.RecoveredOwnerId,NewPhrase);Assert.Equal("Fictional Beauty",active.Store.BusinessName(active.Owner));Assert.Equal(created.Prepared.DatabasePath,new ActiveTestStores(target.Folder).ResolveDatabase());
    }

    [Fact]
    public async Task AChangedSelectionDuringFailureIsPreservedRatherThanDeletedByCleanup()
    {
        using var f=new LedgerFixture();using var target=new TestStore();var created=await Prepare(f,target.Folder);string pointer=Path.Combine(target.Folder,"active-test-store.json");
        var service=new ActiveTestStores(target.Folder){FailurePoint=point=>{if(point=="Selection published"){File.WriteAllText(pointer,"Fictional externally changed selection");throw new IOException("Fictional interrupted activation");}}};
        await Assert.ThrowsAsync<IOException>(()=>service.ActivateAsync(created.Prepared.Receipt.RequestId,f.Business.Owner.OwnerId,NewPhrase));Assert.Equal("Fictional externally changed selection",File.ReadAllText(pointer));Prepared(created.Prepared);
    }

    [Fact]
    public async Task PreparedOwnerLockoutPersistsAcrossActivationAttemptsAndReopening()
    {
        using var f=new LedgerFixture();using var target=new TestStore();var created=await Prepare(f,target.Folder);var r=created.Prepared.Receipt;
        for(int attempt=0;attempt<5;attempt++)await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new ActiveTestStores(target.Folder).ActivateAsync(r.RequestId,r.RecoveredOwnerId,"fictional incorrect activation words"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new ActiveTestStores(target.Folder).ActivateAsync(r.RequestId,r.RecoveredOwnerId,NewPhrase));Prepared(created.Prepared);Assert.False(File.Exists(Path.Combine(target.Folder,"active-test-store.json")));
    }

    [Fact]
    public async Task ASelectionIdentityMismatchDoesNotOpenTheDefaultStore()
    {
        using var f=new LedgerFixture();using var target=new TestStore();var created=await Prepare(f,target.Folder);var service=new ActiveTestStores(target.Folder);var active=await service.ActivateAsync(created.Prepared.Receipt.RequestId,f.Business.Owner.OwnerId,NewPhrase);
        string pointer=Path.Combine(target.Folder,"active-test-store.json");File.WriteAllText(pointer,JsonSerializer.Serialize(active.Selection with{InstanceId=Guid.NewGuid().ToString("N")}));Assert.Throws<InvalidDataException>(()=>service.ResolveDatabase());Assert.False(File.Exists(target.DatabasePath));
    }

    [Fact]
    public async Task ConcurrentPointerReadersObserveCompleteSelectionsDuringReplacement()
    {
        using var target=new TestStore();var paths=new RecoveredStorePaths(target.Folder);string first=Guid.NewGuid().ToString(),second=Guid.NewGuid().ToString();
        ActiveTestSelection Value(string id)=>new(1,id,Guid.NewGuid().ToString(),Guid.NewGuid().ToString("N"),paths.Relative(id),DateTimeOffset.UtcNow.ToString("O"));
        byte[][] values=[JsonSerializer.SerializeToUtf8Bytes(Value(first)),JsonSerializer.SerializeToUtf8Bytes(Value(second))];ActiveStorePointer.Write(paths.Pointer,values[0]);
        using var cancellation=new CancellationTokenSource();var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var failures=new System.Collections.Concurrent.ConcurrentQueue<Exception>();int reads=0;
        var reader=Task.Run(()=>
        {
            while(!cancellation.IsCancellationRequested)
            {
                try{var selection=ActiveStorePointer.Read(paths)!;Assert.True(selection.RequestId==first || selection.RequestId==second);Interlocked.Increment(ref reads);started.TrySetResult();}
                catch(Exception error){failures.Enqueue(error);started.TrySetResult();}
            }
        });
        try{await started.Task.WaitAsync(TimeSpan.FromSeconds(5));for(int index=0;index<40;index++)ActiveStorePointer.Write(paths.Pointer,values[index%2]);}
        finally{cancellation.Cancel();await reader;}
        Assert.Empty(failures);Assert.True(reads>0);Assert.Empty(Directory.EnumerateFiles(target.Folder,"*.partial"));
    }

    [Fact]
    public async Task CancellingFreshProofForTheCurrentStoreDoesNotReturnAnUnlockedSession()
    {
        using var f=new LedgerFixture();using var target=new TestStore();var created=await Prepare(f,target.Folder);var r=created.Prepared.Receipt;await new ActiveTestStores(target.Folder).ActivateAsync(r.RequestId,r.RecoveredOwnerId,NewPhrase);
        using var cancellation=new CancellationTokenSource();var service=new ActiveTestStores(target.Folder){FailurePoint=point=>{if(point=="Owner authenticated")cancellation.Cancel();}};
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.ActivateAsync(r.RequestId,r.RecoveredOwnerId,NewPhrase,cancellation.Token));Assert.Equal(created.Prepared.DatabasePath,service.ResolveDatabase());
    }
}
