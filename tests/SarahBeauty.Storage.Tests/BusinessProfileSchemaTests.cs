using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class BusinessProfileSchemaTests
{
    [Fact]
    public void UpgradePreservesSourceAndSnapshotWithoutInventingPrivateSetupFacts()
    {
        using var f=new BusinessFixture();var quote=QuoteTerminalDecisionTests.Issue(f);long watermark=Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));f.Files.Execute("DROP TABLE BusinessProfileVersions; PRAGMA user_version=38;");
        var upgraded=new BusinessStore(f.Store.DatabasePath);Assert.Equal(39L,TestStore.Scalar(upgraded.DatabasePath,"PRAGMA user_version;"));Assert.Equal(f.Store.InstanceId,upgraded.InstanceId);Assert.Equal(watermark,TestStore.Scalar(upgraded.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Equal(0L,TestStore.Scalar(upgraded.DatabasePath,"SELECT count(*) FROM BusinessProfileVersions;"));Assert.Equal(quote.Hash,Assert.Single(new QuoteBook(upgraded).Versions(f.Owner)).Hash);
        string path=Assert.Single(f.Files.Snapshots());Assert.Equal(38L,TestStore.Scalar(path,"PRAGMA user_version;"));Assert.Equal(0L,TestStore.Scalar(path,"SELECT count(*) FROM sqlite_schema WHERE name='BusinessProfileVersions';"));new BusinessStore(upgraded.DatabasePath);Assert.Single(f.Files.Snapshots());
    }
    [Fact]
    public void RecordingOwnerMustBelongToTheProfileBusiness()
    {
        using var f=new BusinessFixture();string business=Guid.NewGuid().ToString(),owner=Guid.NewGuid().ToString();f.Store.Transaction(db=>db.Execute("INSERT INTO Business(Id,DisplayName) VALUES($business,'Fictional foreign business'); INSERT INTO Owners(Id,BusinessId,Name,Salt,Hash,Iterations) VALUES($owner,$business,'Fictional foreign owner','Fictional structural salt','Fictional structural hash',600000);",("$business",business),("$owner",owner)));
        Assert.Throws<SqliteException>(()=>Insert(f,1,Guid.NewGuid().ToString(),owner));Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM BusinessProfileVersions;"));
    }
    [Fact]
    public void RevisionAndRequestAreUniqueAndHistoryCannotBeUpdatedOrDeleted()
    {
        using var f=new BusinessFixture();string request=Guid.NewGuid().ToString();Insert(f,1,request,f.Owner.OwnerId);Assert.Throws<SqliteException>(()=>Insert(f,1,Guid.NewGuid().ToString(),f.Owner.OwnerId));Assert.Throws<SqliteException>(()=>Insert(f,2,request,f.Owner.OwnerId));
        Assert.Throws<SqliteException>(()=>f.Files.Execute("UPDATE BusinessProfileVersions SET SnapshotJson='{}';"));Assert.Throws<SqliteException>(()=>f.Files.Execute("DELETE FROM BusinessProfileVersions;"));Assert.Equal(1L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM BusinessProfileVersions;"));
    }
    [Fact]
    public void UnsupportedEarlierTableFailsMigrationAndRetainsItsRowAndSnapshot()
    {
        using var f=new BusinessFixture();f.Files.Execute("DROP TABLE BusinessProfileVersions; CREATE TABLE BusinessProfileVersions(Id TEXT); INSERT INTO BusinessProfileVersions VALUES('Fictional preserved unsupported row'); PRAGMA user_version=38;");Assert.Throws<SqliteException>(()=>new BusinessStore(f.Store.DatabasePath));Assert.Equal(38L,TestStore.Scalar(f.Store.DatabasePath,"PRAGMA user_version;"));Assert.Equal("Fictional preserved unsupported row",TestStore.Scalar(f.Store.DatabasePath,"SELECT Id FROM BusinessProfileVersions;"));Assert.Single(f.Files.Snapshots());
    }
    internal static void RemoveEmpty(BusinessFixture f){Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM BusinessProfileVersions;"));f.Files.Execute("DROP TABLE BusinessProfileVersions;");}
    private static void Insert(BusinessFixture f,int revision,string request,string owner)=>f.Store.Transaction(db=>db.Execute("INSERT INTO BusinessProfileVersions(Id,BusinessId,Revision,RequestId,SnapshotJson,SnapshotHash,PayloadHash,RecordedByOwnerId,RecordedAtUtc) VALUES($id,$business,$revision,$request,'{}',$hash,$hash,$owner,'2027-01-01T12:00:00.0000000+00:00');",("$id",Guid.NewGuid().ToString()),("$business",f.Owner.BusinessId),("$revision",revision),("$request",request),("$hash",new string('A',64)),("$owner",owner)));
}
