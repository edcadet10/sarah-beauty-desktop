using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class DocumentDecisionSchemaTests
{
    [Fact]
    public void UpgradeKeepsIssuedVersionsAndSnapshotWithoutInventingADecision()
    {
        using var f=new BusinessFixture();var issued=Issue(f);long watermark=Watermark(f);string instance=f.Store.InstanceId;
        BusinessProfileSchemaTests.RemoveEmpty(f);f.Files.Execute("DROP TABLE DocumentDecisions; DROP INDEX DocumentDecisionDocumentScope; DROP INDEX DocumentDecisionVersionScope; DROP INDEX DocumentDecisionEvidenceScope; PRAGMA user_version=35;");
        var upgraded=new BusinessStore(f.Store.DatabasePath);Assert.Equal((long)BusinessSchema.Version,TestStore.Scalar(upgraded.DatabasePath,"PRAGMA user_version;"));Assert.Equal(instance,upgraded.InstanceId);Assert.Equal(watermark,Watermark(f));
        var retained=Assert.Single(new QuoteBook(upgraded).Versions(f.Owner));Assert.Equal(issued.Id,retained.Id);Assert.Equal(issued.Hash,retained.Hash);Assert.Equal("Issued",retained.Status);Assert.Equal(0L,TestStore.Scalar(upgraded.DatabasePath,"SELECT count(*) FROM DocumentDecisions;"));
        string snapshot=Assert.Single(f.Files.Snapshots());Assert.Equal(35L,TestStore.Scalar(snapshot,"PRAGMA user_version;"));Assert.Equal(0L,TestStore.Scalar(snapshot,"SELECT count(*) FROM sqlite_schema WHERE name='DocumentDecisions';"));Assert.Equal(issued.Hash,TestStore.Scalar(snapshot,"SELECT SnapshotHash FROM DocumentVersions;"));new BusinessStore(upgraded.DatabasePath);Assert.Single(f.Files.Snapshots());
    }
    [Fact]
    public void VersionMustBelongToTheDocumentAndRecordingOwnerMustBelongToTheBusiness()
    {
        using var f=new BusinessFixture();var first=Issue(f);var second=Issue(f);string business=Guid.NewGuid().ToString(),owner=Guid.NewGuid().ToString();
        f.Store.Transaction(db=>db.Execute("INSERT INTO Business(Id,DisplayName) VALUES($business,'Fictional foreign business'); INSERT INTO Owners(Id,BusinessId,Name,Salt,Hash,Iterations) VALUES($owner,$business,'Fictional foreign owner','Fictional structural salt','Fictional structural hash',600000);",("$business",business),("$owner",owner)));
        Assert.Throws<SqliteException>(()=>Insert(f,first,owner));Assert.Throws<SqliteException>(()=>Insert(f,first,f.Owner.OwnerId,second.Id));Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM DocumentDecisions;"));
    }
    [Fact]
    public void AVersionHasOneRetainedDecisionAndCannotBeUpdatedOrDeleted()
    {
        using var f=new BusinessFixture();var version=Issue(f);Insert(f,version,f.Owner.OwnerId);Assert.Throws<SqliteException>(()=>Insert(f,version,f.Owner.OwnerId));
        foreach(string sql in new[]{"UPDATE DocumentDecisions SET SnapshotJson='{}';","DELETE FROM DocumentDecisions;"})Assert.Throws<SqliteException>(()=>f.Store.Transaction(db=>db.Execute(sql)));
        Assert.Equal(1L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM DocumentDecisions;"));Assert.Equal("Issued",Assert.Single(new QuoteBook(f.Store).Versions(f.Owner)).Status);
    }
    [Fact]
    public void UnsupportedEarlierTableCannotBeSilentlyAdoptedByTheUpgrade()
    {
        using var f=new BusinessFixture();var issued=Issue(f);
        BusinessProfileSchemaTests.RemoveEmpty(f);f.Files.Execute("DROP TABLE DocumentDecisions; CREATE TABLE DocumentDecisions(Id TEXT); INSERT INTO DocumentDecisions VALUES('Fictional unsupported retained row'); PRAGMA user_version=35;");
        Assert.Throws<SqliteException>(()=>new BusinessStore(f.Store.DatabasePath));Assert.Equal(35L,TestStore.Scalar(f.Store.DatabasePath,"PRAGMA user_version;"));Assert.Equal("Fictional unsupported retained row",TestStore.Scalar(f.Store.DatabasePath,"SELECT Id FROM DocumentDecisions;"));
        string snapshot=Assert.Single(f.Files.Snapshots());Assert.Equal(35L,TestStore.Scalar(snapshot,"PRAGMA user_version;"));Assert.Equal("Fictional unsupported retained row",TestStore.Scalar(snapshot,"SELECT Id FROM DocumentDecisions;"));Assert.Equal(issued.Hash,TestStore.Scalar(f.Store.DatabasePath,"SELECT SnapshotHash FROM DocumentVersions;"));
    }
    private static DocumentVersion Issue(BusinessFixture f)
    {
        var quotes=new QuoteBook(f.Store);var booking=QuoteTests.Booking(f);var draft=quotes.SaveDraft(f.Owner,quotes.FromBooking(f.Owner,booking));return quotes.Issue(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }
    private static void Insert(BusinessFixture f,DocumentVersion version,string owner,string? versionId=null)=>f.Store.Transaction(db=>db.Execute("""
        INSERT INTO DocumentDecisions(Id,BusinessId,DocumentId,VersionId,RequestId,PayloadHash,SnapshotJson,SnapshotHash,OwnerId,RecordedAtUtc)
        VALUES($id,$business,$document,$version,$request,$hash,'{}',$hash,$owner,$at);
        """,("$id",Guid.NewGuid().ToString()),("$business",f.Owner.BusinessId),("$document",version.DocumentId),("$version",versionId??version.Id),("$request",Guid.NewGuid().ToString()),("$hash",new string('A',64)),("$owner",owner),("$at",DateTimeOffset.UtcNow.ToString("O"))));
    private static long Watermark(BusinessFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));
}
