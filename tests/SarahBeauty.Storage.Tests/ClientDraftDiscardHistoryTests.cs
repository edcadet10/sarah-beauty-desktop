using System.Text.Json;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class ClientDraftDiscardHistoryTests
{
    [Theory]
    [InlineData(DocumentKind.Quote)][InlineData(DocumentKind.Invoice)][InlineData(DocumentKind.Receipt)][InlineData(DocumentKind.CreditNote)]
    public void RetainedHistoryBindsEverySavedKindWithoutChangingIssuedDocumentsOrMoney(DocumentKind kind)
    {
        using var f=new LedgerFixture();string id=Draft(f,kind);var before=f.Business.Store.Read(db=>ArchiveDatabaseChecks.Read(db));var documents=new QuoteBook(f.Business.Store).Versions(f.Business.Owner);var value=Seed(f.Business,kind,id);Assert.Equal(value,Assert.Single(new ClientDraftDiscards(f.Business.Store).History(f.Business.Owner)));var after=f.Business.Store.Read(db=>ArchiveDatabaseChecks.Read(db));Assert.Equal(before.MovementCount,after.MovementCount);Assert.Equal(before.TotalDebits,after.TotalDebits);Assert.Equal(before.TotalCredits,after.TotalCredits);Assert.Equal(1,after.Tables.Single(t=>t.Table=="ClientDraftDiscards").Rows);Assert.Equal(JsonSerializer.Serialize(documents),JsonSerializer.Serialize(new QuoteBook(f.Business.Store).Versions(f.Business.Owner)));
    }
    [Fact]
    public void EmptyHistoryIsProtectedAndCancellationDoesNotWrite()
    {
        using var f=new BusinessFixture();var book=new ClientDraftDiscards(f.Store);Assert.Empty(book.History(f.Owner));using var other=new BusinessFixture();Assert.Throws<UnauthorizedAccessException>(()=>book.History(other.Owner));using var cancellation=new CancellationTokenSource();cancellation.Cancel();Assert.Throws<OperationCanceledException>(()=>book.History(f.Owner,cancellation.Token));f.Owner.Lock();Assert.Throws<UnauthorizedAccessException>(()=>book.History(f.Owner));Assert.Equal(1L,TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));
    }
    [Theory]
    [InlineData("hash")][InlineData("payload")][InlineData("mode")][InlineData("request-index")][InlineData("source")][InlineData("current")][InlineData("missing")][InlineData("issued")]
    public void AlteredRetainedOrCurrentFactsAreRejectedEvenAfterOuterRehashing(string change)
    {
        using var f=new LedgerFixture();string id=Draft(f,DocumentKind.Quote);var value=Seed(f.Business,DocumentKind.Quote,id);f.Business.Files.Execute("DROP TRIGGER ClientDraftDiscards_NoUpdate; DROP TRIGGER ClientDraftDiscards_NoDelete;");
        if(change=="hash")f.Business.Files.Execute("UPDATE ClientDraftDiscards SET SnapshotHash='AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA';");
        else if(change=="payload")f.Business.Files.Execute("UPDATE ClientDraftDiscards SET PayloadHash='BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB';");
        else if(change=="request-index")f.Business.Store.Transaction(db=>db.Execute("UPDATE ClientDraftDiscards SET RequestId=$id;",("$id",Guid.NewGuid().ToString())));
        else if(change=="mode")Replace(f.Business,value with{Mode="LIVE"});
        else if(change=="source")
        {
            var source=ClientDraftSnapshots.Read(value.Kind,value.DraftSnapshotJson) with{Name="Fictional replaced original draft"};string json=ClientDraftSnapshots.Json(source);Replace(f.Business,value with{DraftSnapshotJson=json,DraftSnapshotHash=ClientDraftDiscardCodec.Hash(json)});
        }
        else if(change=="current")f.Business.Store.Transaction(db=>db.Execute("UPDATE Records SET DataJson=json_set(DataJson,'$.Status','Draft') WHERE Id=$id;",("$id",id)));
        else if(change=="issued")f.Business.Store.Transaction(db=>db.Execute("INSERT INTO DocumentVersions(Id,DocumentId,Version,SourceDraftId,IssueDate,DataJson,SnapshotHash,Status,IssuedBy,IssuedAtUtc) SELECT $id,V.DocumentId,(SELECT MAX(W.Version)+1 FROM DocumentVersions W WHERE W.DocumentId=V.DocumentId),$draft,V.IssueDate,V.DataJson,V.SnapshotHash,V.Status,V.IssuedBy,V.IssuedAtUtc FROM DocumentVersions V LIMIT 1;",("$id",Guid.NewGuid().ToString()),("$draft",id)));
        else f.Business.Files.Execute("DELETE FROM ClientDraftDiscards;");
        Assert.Throws<InvalidDataException>(()=>new ClientDraftDiscards(f.Business.Store).History(f.Business.Owner));
    }
    [Fact]
    public void ArchiveCaptureRejectsARehashedWrongModeDiscard()
    {
        using var f=new LedgerFixture();var value=Seed(f.Business,DocumentKind.Quote,Draft(f,DocumentKind.Quote));f.Business.Files.Execute("DROP TRIGGER ClientDraftDiscards_NoUpdate;");Replace(f.Business,value with{Mode="LIVE"});Assert.Throws<InvalidDataException>(()=>{using var captured=new ArchiveSources(f.Business.Store).Capture(f.Business.Owner);});
    }
    [Fact]
    public void AnInactiveHistoricalActorRemainsReadableByTheOtherCurrentOwner()
    {
        using var f=new LedgerFixture();var b=f.Business;var value=Seed(b,DocumentKind.Quote,Draft(f,DocumentKind.Quote));var access=new OwnerAccess(b.Store);var second=access.Owners().Single(o=>o.Id!=b.Owner.OwnerId);var reader=access.SignIn(second.Id,BusinessFixture.Phrase);b.Store.Transaction(db=>db.Execute("UPDATE Owners SET Active=0 WHERE Id=$id;",("$id",b.Owner.OwnerId)));Assert.Equal(value,Assert.Single(new ClientDraftDiscards(b.Store).History(reader)));
    }
    [Fact]
    public async Task EncryptedRecoveryRetainsAllPrivateDiscardFactsWithFreshOwnerAccess()
    {
        using var f=new LedgerFixture();var b=f.Business;using var target=new TestStore();foreach(var kind in Enum.GetValues<DocumentKind>())Seed(b,kind,Draft(f,kind));var expected=new ClientDraftDiscards(b.Store).History(b.Owner);var key=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);string path=Path.Combine(b.Files.Folder,"fictional-private-discards.age");var archive=await new FullArchives(b.Store).CreateAsync(b.Owner,path,key.Recipient);
        using var review=await new ArchiveRestores().StageAsync(path,target.Folder,key.ProtectedIdentity,BusinessFixture.Phrase,archive);const string phrase="fictional meadow orchard restored access";string request=Guid.NewGuid().ToString();var copy=await new RecoveredStores().PrepareAsync(review,target.Folder,new(b.Owner.OwnerId,"Fictional retained discard recovery",true),phrase,request);var active=await new ActiveTestStores(target.Folder).ActivateAsync(request,b.Owner.OwnerId,phrase);Assert.NotEqual(b.Store.InstanceId,active.Store.InstanceId);Assert.Equal(copy.Receipt.InstanceId,active.Store.InstanceId);Assert.Equal(JsonSerializer.Serialize(expected),JsonSerializer.Serialize(new ClientDraftDiscards(active.Store).History(active.Owner)));Assert.Equal(4L,TestStore.Scalar(active.Store.DatabasePath,"SELECT count(*) FROM ClientDraftDiscards;"));
    }
    [Fact]
    public void ReadableGenerationExcludesPrivateDiscardReasonsAndRetainedSnapshots()
    {
        using var f=new LedgerFixture();var b=f.Business;var value=Seed(b,DocumentKind.Quote,Draft(f,DocumentKind.Quote));using var snapshot=new ReadableExportSnapshots(b.Store).Capture(b.Owner);var generation=ReadableExportGeneration.Build(snapshot,DocumentDecisionExportContractTests.Selection);string json=JsonSerializer.Serialize(generation.Tables);Assert.Equal(165,generation.Tables.Count);Assert.DoesNotContain(value.Reason,json,StringComparison.Ordinal);Assert.DoesNotContain(value.DraftSnapshotJson,json,StringComparison.Ordinal);
    }
    internal static string Draft(LedgerFixture f,DocumentKind kind)
    {
        var b=f.Business;var invoice=ApplicationTests.Invoice(f,5000);var invoiceDraft=new InvoiceBook(b.Store).Drafts(b.Owner).Single(d=>d.IssuedVersionId==invoice.Id);var quote=new QuoteBook(b.Store).Versions(b.Owner).Single(v=>v.Id==invoiceDraft.QuoteVersionId);
        return kind switch
        {
            DocumentKind.Quote=>new QuoteBook(b.Store).SaveDraft(b.Owner,new QuoteBook(b.Store).Drafts(b.Owner).Single(d=>d.IssuedVersionId==quote.Id) with{Id="",Revision=0,Status="Draft",IssuedVersionId="",Name="Fictional private unused quote"}).Id,
            DocumentKind.Invoice=>new InvoiceBook(b.Store).SaveDraft(b.Owner,new(){Name="Fictional private unused invoice",QuoteVersionId=quote.Id}).Id,
            DocumentKind.CreditNote=>new CreditNoteBook(b.Store).SaveDraft(b.Owner,new(){Name="Fictional private unused reduction",InvoiceVersionId=invoice.Id}).Id,
            _=>ReceiptDraft(f)
        };
    }
    private static string ReceiptDraft(LedgerFixture f)
    {
        var payment=f.Post(f.Draft(FinancialKind.Receipt,2300));return new ReceiptBook(f.Business.Store).SaveDraft(f.Business.Owner,new(){Name="Fictional private unused receipt",PaymentId=payment.Id,IssueDate=f.Date}).Id;
    }
    internal static ClientDraftDiscard Seed(BusinessFixture f,DocumentKind kind,string id)
    {
        return f.Store.Transaction(db=>
        {
            string original=ClientDraftSnapshots.Current(db,f.Owner.BusinessId,kind,id);var source=ClientDraftSnapshots.Read(kind,original);var value=new ClientDraftDiscard(1,Guid.NewGuid().ToString(),f.Owner.BusinessId,id,kind,source.Revision,Guid.NewGuid().ToString(),"TEST",original,ClientDraftDiscardCodec.Hash(original),"Fictional private discarded-draft reason",f.Owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));string json=JsonSerializer.Serialize(value);var discarded=ClientDraftSnapshots.Discarded(source);
            db.Execute("UPDATE Records SET Revision=$revision,DataJson=$json WHERE Id=$id;",("$revision",discarded.Revision),("$json",ClientDraftSnapshots.Json(discarded)),("$id",id));db.Execute("INSERT INTO ClientDraftDiscards(Id,BusinessId,DraftId,Kind,RecordKind,DraftRevision,RequestId,PayloadHash,SnapshotJson,SnapshotHash,OwnerId,RecordedAtUtc) VALUES($id,$business,$draft,$kind,$record,$revision,$request,$payload,$json,$hash,$owner,$at);",("$id",value.Id),("$business",value.BusinessId),("$draft",value.DraftId),("$kind",kind.ToString()),("$record",ClientDraftSnapshots.RecordKind(kind)),("$revision",value.DraftRevision),("$request",value.RequestId),("$payload",ClientDraftDiscardCodec.Payload(ClientDraftDiscardCodec.Request(value))),("$json",json),("$hash",ClientDraftDiscardCodec.Hash(json)),("$owner",value.OwnerId),("$at",value.RecordedAtUtc));return value;
        });
    }
    private static void Replace(BusinessFixture f,ClientDraftDiscard value)
    {
        string json=JsonSerializer.Serialize(value);f.Store.Transaction(db=>db.Execute("UPDATE ClientDraftDiscards SET SnapshotJson=$json,SnapshotHash=$hash,PayloadHash=$payload;",("$json",json),("$hash",ClientDraftDiscardCodec.Hash(json)),("$payload",ClientDraftDiscardCodec.Payload(ClientDraftDiscardCodec.Request(value)))));
    }
}
