using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class OwnerInterestSchemaTests
{
    [Fact]
    public void UpgradePreservesMoneyIdentityAndAnOpenableVersionThirtyTwoSnapshotWithoutInventingInterests()
    {
        using var f=new LedgerFixture();f.Post(f.Draft(FinancialKind.Receipt,12345));var store=f.Business.Store;string id=store.StoreId,instance=store.InstanceId;long watermark=Convert.ToInt64(TestStore.Scalar(store.DatabasePath,"SELECT Watermark FROM Business;"));
        Downgrade(f.Business);var upgraded=new BusinessStore(store.DatabasePath);Assert.Equal(id,upgraded.StoreId);Assert.Equal(instance,upgraded.InstanceId);Assert.Equal((long)BusinessSchema.Version,TestStore.Scalar(store.DatabasePath,"PRAGMA user_version;"));
        Assert.Equal(watermark,TestStore.Scalar(store.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Equal(12345L,TestStore.Scalar(store.DatabasePath,"SELECT SUM(DebitCents) FROM JournalLines;"));Assert.Equal(12345L,TestStore.Scalar(store.DatabasePath,"SELECT SUM(CreditCents) FROM JournalLines;"));
        Assert.Equal(0L,TestStore.Scalar(store.DatabasePath,"SELECT count(*) FROM OwnerInterestVersions;"));Assert.Equal(0L,TestStore.Scalar(store.DatabasePath,"SELECT count(*) FROM OwnerInterestShares;"));
        string snapshot=Assert.Single(f.Business.Files.Snapshots());Assert.Equal(32L,TestStore.Scalar(snapshot,"PRAGMA user_version;"));Assert.Equal(0L,TestStore.Scalar(snapshot,"SELECT count(*) FROM sqlite_schema WHERE name='OwnerInterestVersions';"));
        Assert.Equal(12345L,TestStore.Scalar(snapshot,"SELECT SUM(DebitCents) FROM JournalLines;"));Assert.NotEmpty(upgraded.BusinessName(f.Business.Owner));new BusinessStore(store.DatabasePath);Assert.Single(f.Business.Files.Snapshots());
    }
    [Fact]
    public void UnsupportedExistingTableRollsBackTheUpgradeAndPreservesItsContentsAndSnapshot()
    {
        using var f=new BusinessFixture();Downgrade(f);f.Files.Execute("CREATE TABLE OwnerInterestVersions(Id TEXT); INSERT INTO OwnerInterestVersions(Id) VALUES('Fictional preserved unsupported row');");
        Assert.Throws<SqliteException>(()=>new BusinessStore(f.Store.DatabasePath));Assert.Equal(32L,TestStore.Scalar(f.Store.DatabasePath,"PRAGMA user_version;"));Assert.Equal("Fictional preserved unsupported row",TestStore.Scalar(f.Store.DatabasePath,"SELECT Id FROM OwnerInterestVersions;"));
        string snapshot=Assert.Single(f.Files.Snapshots());Assert.Equal("Fictional preserved unsupported row",TestStore.Scalar(snapshot,"SELECT Id FROM OwnerInterestVersions;"));Assert.Equal(2L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM Owners;"));
    }
    [Fact]
    public void SharesAndRecordingOwnersMustBelongToTheVersionsBusiness()
    {
        using var f=new BusinessFixture();string foreignBusiness=Guid.NewGuid().ToString(),foreignOwner=Guid.NewGuid().ToString(),version=Guid.NewGuid().ToString("N");
        f.Store.Transaction(db=>db.Execute("INSERT INTO Business(Id,DisplayName) VALUES($business,'Fictional foreign business'); INSERT INTO Owners(Id,BusinessId,Name,Salt,Hash,Iterations) VALUES($owner,$business,'Fictional foreign owner','Fictional salt','Fictional hash',600000);",("$business",foreignBusiness),("$owner",foreignOwner)));
        Assert.Throws<SqliteException>(()=>Insert(f,Guid.NewGuid().ToString("N"),foreignOwner));Insert(f,version,f.Owner.OwnerId);
        Assert.Throws<SqliteException>(()=>f.Store.Transaction(db=>db.Execute("INSERT INTO OwnerInterestShares(VersionId,BusinessId,OwnerId,OwnerNameAtConfirmation,FractionText) VALUES($version,$business,$owner,'Fictional owner','0.5');",("$version",version),("$business",f.Owner.BusinessId),("$owner",foreignOwner))));
        Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM OwnerInterestShares;"));
    }
    [Fact]
    public void ConfirmationsAndSharesRejectUpdatesAndDeletes()
    {
        using var f=new BusinessFixture();string version=Guid.NewGuid().ToString();Insert(f,version,f.Owner.OwnerId);
        f.Store.Transaction(db=>db.Execute("INSERT INTO OwnerInterestShares(VersionId,BusinessId,OwnerId,OwnerNameAtConfirmation,FractionText) VALUES($version,$business,$owner,'Fictional owner','1');",("$version",version),("$business",f.Owner.BusinessId),("$owner",f.Owner.OwnerId)));
        foreach(string statement in new[]{"UPDATE OwnerInterestVersions SET SnapshotJson='changed';","DELETE FROM OwnerInterestVersions;","UPDATE OwnerInterestShares SET FractionText='0';","DELETE FROM OwnerInterestShares;"})
            Assert.Throws<SqliteException>(()=>f.Store.Transaction(db=>db.Execute(statement)));
        Assert.Equal("1",TestStore.Scalar(f.Store.DatabasePath,"SELECT FractionText FROM OwnerInterestShares;"));Assert.Equal("{}",TestStore.Scalar(f.Store.DatabasePath,"SELECT SnapshotJson FROM OwnerInterestVersions;"));
    }
    private static void Insert(BusinessFixture f,string id,string owner)=>f.Store.Transaction(db=>db.Execute("INSERT INTO OwnerInterestVersions(Id,BusinessId,Revision,EffectiveFrom,RecordedByOwnerId,RecordedAtUtc,SnapshotJson,SnapshotHash) VALUES($id,$business,1,'2027-01-01',$owner,$now,'{}',$hash);",
        ("$id",id),("$business",f.Owner.BusinessId),("$owner",owner),("$now",DateTimeOffset.UtcNow.ToString("O")),("$hash",new string('A',64))));
    private static void Downgrade(BusinessFixture f)
    {
        if(Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM DocumentDecisions;"))!=0)
            throw new InvalidOperationException("An older ownership-layout fixture cannot discard retained document decisions.");
        f.Files.Execute("DROP TABLE DocumentDecisions; DROP INDEX DocumentDecisionDocumentScope; DROP INDEX DocumentDecisionVersionScope; DROP INDEX DocumentDecisionEvidenceScope; DROP TABLE OwnerInterestShares; DROP TABLE OwnerInterestVersions; DROP INDEX OwnerInterestOwnerScope; PRAGMA user_version=32;");
    }
}
