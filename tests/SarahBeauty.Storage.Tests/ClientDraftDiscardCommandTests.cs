using System.Text.Json;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class ClientDraftDiscardCommandTests
{
    [Theory]
    [InlineData(DocumentKind.Quote)][InlineData(DocumentKind.Invoice)][InlineData(DocumentKind.Receipt)][InlineData(DocumentKind.CreditNote)]
    public void DiscardIsAtomicRetainedAndRetryableWithoutChangingIssuedFactsOrMoney(DocumentKind kind)
    {
        using var f=new LedgerFixture();var b=f.Business;string id=ClientDraftDiscardHistoryTests.Draft(f,kind);var book=new ClientDraftDiscards(b.Store);var request=book.Review(b.Owner,kind,id," Fictional unused client draft ");string original=b.Store.Read(db=>ClientDraftSnapshots.Current(db,b.Owner.BusinessId,kind,id));string documents=JsonSerializer.Serialize(new QuoteBook(b.Store).Versions(b.Owner));string financial=FinancialState(b);long watermark=Watermark(b),movements=Count(b,"Movements"),journal=Count(b,"JournalLines");string identity=Guid.NewGuid().ToString();var result=book.Discard(b.Owner,request,BusinessFixture.Phrase,identity);
        Assert.Equal(original,result.DraftSnapshotJson);Assert.Equal("TEST",result.Mode);Assert.Equal("Fictional unused client draft",result.Reason);Assert.Equal(b.Owner.OwnerId,result.OwnerId);Assert.Equal(watermark+1,Watermark(b));Assert.Equal(movements,Count(b,"Movements"));Assert.Equal(journal,Count(b,"JournalLines"));Assert.Equal(documents,JsonSerializer.Serialize(new QuoteBook(b.Store).Versions(b.Owner)));Assert.Equal(1L,Count(b,"ClientDraftDiscards"));Assert.Equal(result,book.Discard(b.Owner,request,BusinessFixture.Phrase,identity));Assert.Equal(watermark+1,Watermark(b));Assert.Throws<ArgumentException>(()=>book.Discard(b.Owner,request,BusinessFixture.Phrase,Guid.NewGuid().ToString()));
        var source=ClientDraftSnapshots.Read(kind,original) with{Revision=request.ExpectedRevision+1};Assert.Throws<ArgumentException>(()=>Save(b,source));Assert.Throws<ArgumentException>(()=>Issue(b.Store,b.Owner,kind,id,source.Revision));Assert.Equal(watermark+1,Watermark(b));Assert.Equal(financial,FinancialState(b));
    }
    [Fact]
    public void WrongProofForeignOwnerAndPreCancellationCannotDiscard()
    {
        using var f=new LedgerFixture();var b=f.Business;string id=ClientDraftDiscardHistoryTests.Draft(f,DocumentKind.Quote);var book=new ClientDraftDiscards(b.Store);var request=book.Review(b.Owner,DocumentKind.Quote,id,"Fictional discard review");long watermark=Watermark(b);Assert.Throws<UnauthorizedAccessException>(()=>book.Discard(b.Owner,request,"fictional wrong owner words",Guid.NewGuid().ToString()));using var foreign=new BusinessFixture();Assert.Throws<UnauthorizedAccessException>(()=>book.Discard(foreign.Owner,request,BusinessFixture.Phrase,Guid.NewGuid().ToString()));using var cancellation=new CancellationTokenSource();cancellation.Cancel();Assert.Throws<OperationCanceledException>(()=>book.Discard(b.Owner,request,BusinessFixture.Phrase,Guid.NewGuid().ToString(),cancellation.Token));Assert.Equal(watermark,Watermark(b));Assert.Equal(0L,Count(b,"ClientDraftDiscards"));
    }
    [Theory]
    [InlineData("revision")][InlineData("hash")]
    public void ChangedReviewedContentIsRejected(string change)
    {
        using var f=new LedgerFixture();var b=f.Business;string id=ClientDraftDiscardHistoryTests.Draft(f,DocumentKind.Quote);var book=new ClientDraftDiscards(b.Store);var request=book.Review(b.Owner,DocumentKind.Quote,id,"Fictional exact draft review");if(change=="revision")Save(b,b.Store.Read(db=>ClientDraftSnapshots.Read(DocumentKind.Quote,ClientDraftSnapshots.Current(db,b.Owner.BusinessId,DocumentKind.Quote,id))) with{Name="Fictional newer draft"});else b.Store.Transaction(db=>db.Execute("UPDATE Records SET DataJson=json_set(DataJson,'$.Name','Fictional changed same revision') WHERE Id=$id;",("$id",id)));long watermark=Watermark(b);Assert.Throws<ArgumentException>(()=>book.Discard(b.Owner,request,BusinessFixture.Phrase,Guid.NewGuid().ToString()));Assert.Equal(watermark,Watermark(b));Assert.Equal(0L,Count(b,"ClientDraftDiscards"));
    }
    [Theory]
    [InlineData("io")][InlineData("cancel")][InlineData("lock")]
    public void PostWriteFailureCancellationOrLockRollsBackEveryDiscardFact(string failure)
    {
        using var f=new LedgerFixture();var b=f.Business;string id=ClientDraftDiscardHistoryTests.Draft(f,DocumentKind.Quote);using var cancellation=new CancellationTokenSource();var book=new ClientDraftDiscards(b.Store){BeforeDiscardCommit=()=>{if(failure=="io")throw new IOException("Fictional discard storage failure");if(failure=="cancel")cancellation.Cancel();else b.Owner.Lock();}};var request=book.Review(b.Owner,DocumentKind.Quote,id,"Fictional rollback review");string original=b.Store.Read(db=>ClientDraftSnapshots.Current(db,b.Owner.BusinessId,DocumentKind.Quote,id));long watermark=Watermark(b);
        if(failure=="io")Assert.Throws<IOException>(()=>book.Discard(b.Owner,request,BusinessFixture.Phrase,Guid.NewGuid().ToString(),cancellation.Token));else if(failure=="cancel")Assert.Throws<OperationCanceledException>(()=>book.Discard(b.Owner,request,BusinessFixture.Phrase,Guid.NewGuid().ToString(),cancellation.Token));else Assert.Throws<UnauthorizedAccessException>(()=>book.Discard(b.Owner,request,BusinessFixture.Phrase,Guid.NewGuid().ToString(),cancellation.Token));Assert.Equal(watermark,Watermark(b));Assert.Equal(0L,Count(b,"ClientDraftDiscards"));Assert.Equal(original,b.Store.Read(db=>ClientDraftSnapshots.Current(db,b.Owner.BusinessId,DocumentKind.Quote,id)));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void ACompetingEditOrIssuanceBeforeTheWriterReservationWinsInsteadOfBeingDiscarded(bool issue)
    {
        using var f=new LedgerFixture();var b=f.Business;string id=ClientDraftDiscardHistoryTests.Draft(f,DocumentKind.Quote);if(issue){var quotes=new QuoteBook(b.Store);var draft=quotes.Drafts(b.Owner).Single(d=>d.Id==id);var booking=new BookingBook(b.Store).List(b.Owner).Single(v=>v.Id==draft.BookingId);quotes.SaveDraft(b.Owner,draft with{BookingRevision=booking.Revision});}var book=new ClientDraftDiscards(b.Store);var request=book.Review(b.Owner,DocumentKind.Quote,id,"Fictional concurrent review");int reservations=0;bool competed=false;b.Store.BeforeTransactionReservation=()=>
        {
            if(++reservations!=2)return;b.Store.BeforeTransactionReservation=null;var other=new BusinessStore(b.Store.DatabasePath);if(issue)Issue(other,b.Owner,DocumentKind.Quote,id,request.ExpectedRevision);else{var value=other.Read(db=>ClientDraftSnapshots.Read(DocumentKind.Quote,ClientDraftSnapshots.Current(db,b.Owner.BusinessId,DocumentKind.Quote,id)));new QuoteBook(other).SaveDraft(b.Owner,((DocumentDraft)value) with{Name="Fictional competing edited draft"});}competed=true;
        };Assert.Throws<ArgumentException>(()=>book.Discard(b.Owner,request,BusinessFixture.Phrase,Guid.NewGuid().ToString()));Assert.True(competed);Assert.Equal(0L,Count(b,"ClientDraftDiscards"));
    }
    [Fact]
    public void PermanentRequestCannotBeReusedForChangedPayloadAndAnotherOwnerCanReadTheExactRetry()
    {
        using var f=new LedgerFixture();var b=f.Business;string id=ClientDraftDiscardHistoryTests.Draft(f,DocumentKind.Quote);var book=new ClientDraftDiscards(b.Store);var request=book.Review(b.Owner,DocumentKind.Quote,id,"Fictional permanent request");string identity=Guid.NewGuid().ToString();var saved=book.Discard(b.Owner,request,BusinessFixture.Phrase,identity);long watermark=Watermark(b);Assert.Throws<ArgumentException>(()=>book.Discard(b.Owner,request with{Reason="Fictional changed reason"},BusinessFixture.Phrase,identity));var access=new OwnerAccess(b.Store);var second=access.SignIn(access.Owners().Single(o=>o.Id!=b.Owner.OwnerId).Id,BusinessFixture.Phrase);Assert.Equal(saved,book.Discard(second,request,BusinessFixture.Phrase,identity));Assert.Equal(watermark,Watermark(b));
    }
    [Fact]
    public async Task TwoIndependentDiscardRequestsCanRecordOnlyOneFactAndAudit()
    {
        using var f=new LedgerFixture();var b=f.Business;string id=ClientDraftDiscardHistoryTests.Draft(f,DocumentKind.Quote);var request=new ClientDraftDiscards(b.Store).Review(b.Owner,DocumentKind.Quote,id,"Fictional competing discard requests");long watermark=Watermark(b);var attempts=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(()=>{try{new ClientDraftDiscards(new BusinessStore(b.Store.DatabasePath)).Discard(b.Owner,request,BusinessFixture.Phrase,Guid.NewGuid().ToString());return true;}catch(ArgumentException){return false;}})));Assert.Single(attempts,v=>v);Assert.Equal(1L,Count(b,"ClientDraftDiscards"));Assert.Equal(watermark+1,Watermark(b));
    }
    private static string FinancialState(BusinessFixture f)=>f.Store.Read(db=>JsonSerializer.Serialize(new{Movements=db.Read("SELECT Id,DataJson FROM Movements ORDER BY Id;",r=>new[]{r.GetString(0),r.GetString(1)}),Journal=db.Read("SELECT Id,MovementId,AccountId,DebitCents,CreditCents FROM JournalLines ORDER BY Id;",r=>new object[]{r.GetString(0),r.GetString(1),r.GetString(2),r.GetInt64(3),r.GetInt64(4)})}));
    private static long Watermark(BusinessFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));
    private static long Count(BusinessFixture f,string table)=>Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM "+table+";"));
    private static BusinessRecord Save(BusinessFixture f,BusinessRecord source)=>source switch{DocumentDraft v=>new QuoteBook(f.Store).SaveDraft(f.Owner,v),InvoiceDraft v=>new InvoiceBook(f.Store).SaveDraft(f.Owner,v),ReceiptDraft v=>new ReceiptBook(f.Store).SaveDraft(f.Owner,v),CreditNoteDraft v=>new CreditNoteBook(f.Store).SaveDraft(f.Owner,v),_=>throw new ArgumentException()};
    private static DocumentVersion Issue(BusinessStore store,OwnerSession owner,DocumentKind kind,string id,int revision)=>kind switch{DocumentKind.Quote=>new QuoteBook(store).Issue(owner,id,revision,BusinessFixture.Phrase),DocumentKind.Invoice=>new InvoiceBook(store).Issue(owner,id,revision,BusinessFixture.Phrase),DocumentKind.Receipt=>new ReceiptBook(store).Issue(owner,id,revision,BusinessFixture.Phrase),_=>new CreditNoteBook(store).Issue(owner,id,revision,BusinessFixture.Phrase)};
}
