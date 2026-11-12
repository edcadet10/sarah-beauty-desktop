using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Storage;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Ownership;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class RecoveredUpgradeInventoryTests
{
    private const string NewPhrase="fictional orchard lantern river";
    [Theory]
    [InlineData(31)][InlineData(35)]
    public async Task FaithfulOlderArchiveCanUpgradeAndActivateWithOnlyKnownEmptyNewTables(int version)
    {
        using var ledger=new LedgerFixture();var f=ledger.Business;ledger.Post(ledger.Draft(FinancialKind.Receipt,12345));using var legacy=new TestStore();using var target=new TestStore();var older=await StoreInstanceTests.EarlierArchive(f,legacy,version);
        Assert.Equal(0L,TestStore.Scalar(legacy.DatabasePath,"SELECT count(*) FROM sqlite_schema WHERE name='DocumentDecisions';"));
        if(version==31)Assert.Equal(0L,TestStore.Scalar(legacy.DatabasePath,"SELECT count(*) FROM sqlite_schema WHERE name IN ('OwnerInterestVersions','OwnerInterestShares');"));
        using var review=await new ArchiveRestores().StageAsync(older.Archive,legacy.Folder,older.Key.ProtectedIdentity,BusinessFixture.Phrase);string request=Guid.NewGuid().ToString();
        var prepared=await new RecoveredStores().PrepareAsync(review,target.Folder,new(f.Owner.OwnerId,"Fictional earlier-schema activation review",true),NewPhrase,request);
        var activated=await new ActiveTestStores(target.Folder).ActivateAsync(request,f.Owner.OwnerId,NewPhrase);Assert.Equal("Fictional Beauty",activated.Store.BusinessName(activated.Owner));Assert.Equal(prepared.Receipt.InstanceId,activated.Store.InstanceId);
        Assert.Equal((long)BusinessSchema.Version,TestStore.Scalar(activated.Store.DatabasePath,"PRAGMA user_version;"));Assert.Equal(0L,TestStore.Scalar(activated.Store.DatabasePath,"SELECT count(*) FROM DocumentDecisions;"));Assert.Equal(0L,TestStore.Scalar(activated.Store.DatabasePath,"SELECT count(*) FROM OwnerInterestVersions;"));Assert.Equal(prepared.Receipt.SourceWatermark+2,TestStore.Scalar(activated.Store.DatabasePath,"SELECT Watermark FROM Business;"));
        Assert.Equal(12345L,TestStore.Scalar(activated.Store.DatabasePath,"SELECT SUM(DebitCents) FROM JournalLines;"));Assert.Equal(12345L,TestStore.Scalar(activated.Store.DatabasePath,"SELECT SUM(CreditCents) FROM JournalLines;"));Assert.Equal(1L,TestStore.Scalar(activated.Store.DatabasePath,"SELECT count(*) FROM Movements;"));
        Assert.Equal((long)version,TestStore.Scalar(review.FilePath("sarahbeauty.db"),"PRAGMA user_version;"));Assert.Equal("Fictional Beauty",f.Store.BusinessName(f.Owner));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task UnexpectedTablesOrNewMigrationRowsCannotBeHiddenAsAnUpgrade(bool addRows)
    {
        using var f=new BusinessFixture();using var legacy=new TestStore();using var target=new TestStore();var older=await StoreInstanceTests.EarlierArchive(f,legacy);
        using var review=await new ArchiveRestores().StageAsync(older.Archive,legacy.Folder,older.Key.ProtectedIdentity,BusinessFixture.Phrase);string request=Guid.NewGuid().ToString();
        var prepared=await new RecoveredStores().PrepareAsync(review,target.Folder,new(f.Owner.OwnerId,"Fictional changed-upgrade review",true),NewPhrase,request);var copy=new BusinessStore(prepared.DatabasePath,"Prepared");
        if(addRows)
        {
            var owner=new OwnerAccess(copy).SignIn(f.Owner.OwnerId,NewPhrase);var shares=new OwnerInterests(copy).Owners(owner).Select(o=>new OwnerInterestInput(o.Id,o.Id==owner.OwnerId ? .6m : .4m)).ToArray();
            new OwnerInterests(copy).Confirm(owner,new(0,new(2027,1,1),shares,"Fictional inserted ownership after recovery"));
            copy.Transaction(db=>db.Execute("UPDATE Business SET Watermark=$watermark; DELETE FROM Audit WHERE Action='Confirmed dated ownership percentages';",("$watermark",prepared.Receipt.SourceWatermark+1)));
        }
        else copy.Transaction(db=>db.Execute("CREATE TABLE FictionalUnexpectedTable(Id TEXT);"));
        await Assert.ThrowsAsync<InvalidDataException>(()=>new ActiveTestStores(target.Folder).ActivateAsync(request,f.Owner.OwnerId,NewPhrase));
        Assert.Equal("Prepared",TestStore.Scalar(prepared.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='WritableInstanceState';"));Assert.False(File.Exists(Path.Combine(target.Folder,"active-test-store.json")));Assert.Equal("Fictional Beauty",f.Store.BusinessName(f.Owner));
    }
}
