using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Setup;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class ClientDraftDiscardSchemaTests
{
    [Fact]
    public void UpgradeRetainsTheVerifiedPriorStoreWithoutInventingDiscardFacts()
    {
        using var ledger=new LedgerFixture();var f=ledger.Business;ledger.Post(ledger.Draft(FinancialKind.Receipt,2300));var invoice=ApplicationTests.Invoice(ledger,5000);var profile=new BusinessProfiles(f.Store).Confirm(f.Owner,new(0,new(){DisplayName=f.Store.BusinessName(f.Owner)},"Fictional unknown profile retained during migration"),BusinessFixture.Phrase,Guid.NewGuid().ToString());long watermark=Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));RemoveEmpty(f);f.Files.Execute("PRAGMA user_version=39;");
        var upgraded=new BusinessStore(f.Store.DatabasePath);Assert.Equal((long)BusinessSchema.Version,TestStore.Scalar(upgraded.DatabasePath,"PRAGMA user_version;"));Assert.Equal(f.Store.InstanceId,upgraded.InstanceId);Assert.Equal(watermark,TestStore.Scalar(upgraded.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Equal(0L,TestStore.Scalar(upgraded.DatabasePath,"SELECT count(*) FROM ClientDraftDiscards;"));Assert.Equal(profile,new BusinessProfiles(upgraded).Current(f.Owner));Assert.Contains(new QuoteBook(upgraded).Versions(f.Owner),v=>v.Hash==invoice.Hash);Assert.Equal(2300L,TestStore.Scalar(upgraded.DatabasePath,"SELECT SUM(DebitCents) FROM JournalLines;"));
        string snapshot=Assert.Single(f.Files.Snapshots());Assert.Equal(39L,TestStore.Scalar(snapshot,"PRAGMA user_version;"));Assert.Equal(0L,TestStore.Scalar(snapshot,"SELECT count(*) FROM sqlite_schema WHERE name='ClientDraftDiscards';"));new BusinessStore(upgraded.DatabasePath);Assert.Single(f.Files.Snapshots());
    }
    [Theory]
    [InlineData(DocumentKind.Quote)][InlineData(DocumentKind.Invoice)][InlineData(DocumentKind.Receipt)][InlineData(DocumentKind.CreditNote)]
    public void EveryClientKindHasATypeBoundRetainedRow(DocumentKind kind)
    {
        using var f=new BusinessFixture();string draft=Source(f,kind);Insert(f,draft,kind,RecordKind(kind));Assert.Equal(1L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM ClientDraftDiscards;"));
    }
    [Fact]
    public void WrongTypeForeignBusinessAndForeignOwnerCannotBeRecorded()
    {
        using var f=new BusinessFixture();string draft=Source(f,DocumentKind.Quote),business=Guid.NewGuid().ToString(),owner=Guid.NewGuid().ToString();f.Store.Transaction(db=>db.Execute("INSERT INTO Business(Id,DisplayName) VALUES($business,'Fictional foreign business'); INSERT INTO Owners(Id,BusinessId,Name,Salt,Hash,Iterations) VALUES($owner,$business,'Fictional discard foreign owner','Fictional structural salt','Fictional structural hash',600000);",("$business",business),("$owner",owner)));
        Assert.Throws<SqliteException>(()=>Insert(f,draft,DocumentKind.Invoice,"InvoiceDraft"));Assert.Throws<SqliteException>(()=>Insert(f,draft,DocumentKind.Quote,"InvoiceDraft"));Assert.Throws<SqliteException>(()=>Insert(f,draft,DocumentKind.Quote,"DocumentDraft",business:business,owner:owner));Assert.Throws<SqliteException>(()=>Insert(f,draft,DocumentKind.Quote,"DocumentDraft",owner:owner));Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM ClientDraftDiscards;"));
    }
    [Fact]
    public void DraftAndRequestAreUniqueAndRecordedHistoryIsAppendOnly()
    {
        using var f=new BusinessFixture();string first=Source(f,DocumentKind.Quote),second=Source(f,DocumentKind.Quote),request=Guid.NewGuid().ToString();Insert(f,first,DocumentKind.Quote,"DocumentDraft",request:request);Assert.Throws<SqliteException>(()=>Insert(f,first,DocumentKind.Quote,"DocumentDraft"));Assert.Throws<SqliteException>(()=>Insert(f,second,DocumentKind.Quote,"DocumentDraft",request:request));Assert.Throws<SqliteException>(()=>f.Files.Execute("UPDATE ClientDraftDiscards SET SnapshotJson='{}';"));Assert.Throws<SqliteException>(()=>f.Files.Execute("DELETE FROM ClientDraftDiscards;"));Assert.Equal(1L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM ClientDraftDiscards;"));
    }
    [Fact]
    public void AnUnsupportedEarlierTableFailsWithoutReplacingItsRowOrSnapshot()
    {
        using var f=new BusinessFixture();RemoveEmpty(f);f.Files.Execute("CREATE TABLE ClientDraftDiscards(Id TEXT); INSERT INTO ClientDraftDiscards VALUES('Fictional unsupported retained row'); PRAGMA user_version=39;");Assert.Throws<SqliteException>(()=>new BusinessStore(f.Store.DatabasePath));Assert.Equal(39L,TestStore.Scalar(f.Store.DatabasePath,"PRAGMA user_version;"));Assert.Equal("Fictional unsupported retained row",TestStore.Scalar(f.Store.DatabasePath,"SELECT Id FROM ClientDraftDiscards;"));string snapshot=Assert.Single(f.Files.Snapshots());Assert.Equal("Fictional unsupported retained row",TestStore.Scalar(snapshot,"SELECT Id FROM ClientDraftDiscards;"));
    }
    [Fact]
    public void AnOlderFixtureCannotEraseRetainedDiscardHistory()
    {
        using var f=new BusinessFixture();string draft=Source(f,DocumentKind.Quote);Insert(f,draft,DocumentKind.Quote,"DocumentDraft");Assert.Throws<InvalidOperationException>(()=>RemoveEmpty(f));Assert.Equal(1L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM ClientDraftDiscards;"));
    }
    internal static void RemoveEmpty(BusinessFixture f)=>RemoveEmpty(f.Files);
    internal static void RemoveEmpty(TestStore f)
    {
        if(Convert.ToInt64(TestStore.Scalar(f.DatabasePath,"SELECT count(*) FROM sqlite_schema WHERE name='ClientDraftDiscards';"))==0)return;
        if(Convert.ToInt64(TestStore.Scalar(f.DatabasePath,"SELECT count(*) FROM ClientDraftDiscards;"))!=0)throw new InvalidOperationException("An older-layout fixture cannot discard retained client draft history.");f.Execute("DROP TABLE ClientDraftDiscards; DROP INDEX ClientDraftRecordScope;");
    }
    private static string Source(BusinessFixture f,DocumentKind kind)
    {
        BusinessRecord source=kind switch{DocumentKind.Quote=>new DocumentDraft(),DocumentKind.Invoice=>new InvoiceDraft(),DocumentKind.Receipt=>new ReceiptDraft(),_=>new CreditNoteDraft()};source=source with{Id=Guid.NewGuid().ToString(),Revision=1,Name="Fictional structural draft"};string json=System.Text.Json.JsonSerializer.Serialize(source,source.GetType());f.Store.Transaction(db=>db.Execute("INSERT INTO Records(Id,BusinessId,Kind,Revision,DataJson,CreatedAtUtc,CreatedBy,UpdatedAtUtc,UpdatedBy) VALUES($id,$business,$kind,1,$json,$at,$owner,$at,$owner);",("$id",source.Id),("$business",f.Owner.BusinessId),("$kind",RecordKind(kind)),("$json",json),("$at",DateTimeOffset.UtcNow.ToString("O")),("$owner",f.Owner.OwnerId)));return source.Id;
    }
    private static string RecordKind(DocumentKind kind)=>kind switch{DocumentKind.Quote=>"DocumentDraft",DocumentKind.Invoice=>"InvoiceDraft",DocumentKind.Receipt=>"ReceiptDraft",_=>"CreditNoteDraft"};
    private static void Insert(BusinessFixture f,string draft,DocumentKind kind,string recordKind,string? request=null,string? business=null,string? owner=null)=>f.Store.Transaction(db=>db.Execute("INSERT INTO ClientDraftDiscards(Id,BusinessId,DraftId,Kind,RecordKind,DraftRevision,RequestId,PayloadHash,SnapshotJson,SnapshotHash,OwnerId,RecordedAtUtc) VALUES($id,$business,$draft,$kind,$record,1,$request,$hash,'{}',$hash,$owner,$at);",("$id",Guid.NewGuid().ToString()),("$business",business??f.Owner.BusinessId),("$draft",draft),("$kind",kind.ToString()),("$record",recordKind),("$request",request??Guid.NewGuid().ToString()),("$hash",new string('A',64)),("$owner",owner??f.Owner.OwnerId),("$at",DateTimeOffset.UtcNow.ToString("O"))));
}
