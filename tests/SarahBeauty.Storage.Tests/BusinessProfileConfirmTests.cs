using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Setup;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class BusinessProfileConfirmTests
{
    [Fact]
    public void DuplicatePermanentRequestsInAnAlteredStoreRejectHistoryAndArchiveCapture()
    {
        using var f=new BusinessFixture();var book=new BusinessProfiles(f.Store);var first=book.Confirm(f.Owner,Draft(),BusinessFixture.Phrase,Guid.NewGuid().ToString());var second=book.Confirm(f.Owner,Draft(1),BusinessFixture.Phrase,Guid.NewGuid().ToString());var changed=second with{RequestId=first.RequestId};string json=System.Text.Json.JsonSerializer.Serialize(changed);f.Files.Execute("DROP INDEX BusinessProfileRequestOnce; DROP TRIGGER BusinessProfileVersions_NoUpdate;");f.Store.Transaction(db=>db.Execute("UPDATE BusinessProfileVersions SET RequestId=$request,SnapshotJson=$json,SnapshotHash=$hash WHERE Id=$id;",("$request",first.RequestId),("$json",json),("$hash",BusinessProfileCodec.Hash(json)),("$id",second.Id)));
        Assert.Throws<InvalidDataException>(()=>book.History(f.Owner));Assert.Throws<InvalidDataException>(()=>{using var source=new SarahBeauty.Business.Recovery.ArchiveSources(f.Store).Capture(f.Owner);});
    }
    internal static BusinessProfileChange Draft(long revision=0)=>new(revision,new(){DisplayName="Fictional reviewed beauty",LegalName="Fictional reviewed legal entity",Currency="USD",TimeZoneId="UTC",NewBusinessOnlyConfirmed=true},"Fictional private profile review");
    private static long Count(BusinessFixture f,string sql)=>Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,sql));
    [Fact]
    public void ConfirmationAndExactRetryRetainOneReviewWithoutChangingIssuedDocumentsOrMoney()
    {
        using var f=new BusinessFixture();var quote=QuoteTerminalDecisionTests.Issue(f);long watermark=Count(f,"SELECT Watermark FROM Business;");var book=new BusinessProfiles(f.Store);var draft=Draft();string request=Guid.NewGuid().ToString();var saved=book.Confirm(f.Owner,draft,BusinessFixture.Phrase,request);Assert.Equal(saved,book.Confirm(f.Owner,draft,BusinessFixture.Phrase,request));Assert.Equal(saved,Assert.Single(new BusinessProfiles(new BusinessStore(f.Store.DatabasePath)).History(f.Owner)));Assert.Equal("Fictional reviewed beauty",f.Store.BusinessName(f.Owner));Assert.Equal(watermark+1,Count(f,"SELECT Watermark FROM Business;"));Assert.Equal(1,Count(f,"SELECT count(*) FROM Audit WHERE Action='Confirm private business profile';"));
        var retained=Assert.Single(new QuoteBook(f.Store).Versions(f.Owner));Assert.Equal(quote.Hash,retained.Hash);Assert.Equal("Fictional Beauty",retained.Snapshot.BusinessName);Assert.Equal(0,Count(f,"SELECT count(*) FROM Movements;"));Assert.Equal(0,Count(f,"SELECT count(*) FROM JournalLines;"));Assert.Null(saved.Profile.BooksStartDate);Assert.Equal(BusinessSetupState.Unconfirmed,saved.Profile.Tax.State);
        Assert.Throws<ArgumentException>(()=>book.Confirm(f.Owner,draft with{Reason="Fictional conflicting reuse"},BusinessFixture.Phrase,request));var later=book.Confirm(f.Owner,draft with{ExpectedRevision=1,Profile=draft.Profile with{DisplayName="Fictional later review"},Reason="Fictional later private review"},BusinessFixture.Phrase,Guid.NewGuid().ToString());Assert.Equal(saved,book.Confirm(f.Owner,draft,BusinessFixture.Phrase,request));Assert.Equal(later,book.Current(f.Owner));Assert.Equal(watermark+2,Count(f,"SELECT Watermark FROM Business;"));
    }
    [Fact]
    public void WrongPhraseForeignOwnerStaleRevisionAndCancelledInputCannotCreateAProfile()
    {
        using var f=new BusinessFixture();using var foreign=new BusinessFixture();var book=new BusinessProfiles(f.Store);long watermark=Count(f,"SELECT Watermark FROM Business;");Assert.Throws<UnauthorizedAccessException>(()=>book.Confirm(f.Owner,Draft(),"fictional wrong phrase",Guid.NewGuid().ToString()));Assert.Throws<UnauthorizedAccessException>(()=>book.Confirm(foreign.Owner,Draft(),BusinessFixture.Phrase,Guid.NewGuid().ToString()));Assert.Throws<ArgumentException>(()=>book.Confirm(f.Owner,Draft(1),BusinessFixture.Phrase,Guid.NewGuid().ToString()));using var cancel=new CancellationTokenSource();cancel.Cancel();Assert.Throws<OperationCanceledException>(()=>book.Confirm(f.Owner,Draft(),BusinessFixture.Phrase,Guid.NewGuid().ToString(),cancel.Token));Assert.Null(book.Current(f.Owner));Assert.Equal(watermark,Count(f,"SELECT Watermark FROM Business;"));
    }
    [Theory]
    [InlineData("cancel")][InlineData("lock")][InlineData("storage")]
    public void FailureAfterWritingRollsBackHistoryNameAndAudit(string failure)
    {
        using var f=new BusinessFixture();long watermark=Count(f,"SELECT Watermark FROM Business;");using var cancel=new CancellationTokenSource();bool reached=false;var book=new BusinessProfiles(f.Store){BeforeProfileCommit=()=>{reached=true;if(failure=="cancel")cancel.Cancel();else if(failure=="lock")f.Owner.Lock();else throw new IOException("Fictional post-write profile failure");}};
        var error=Record.Exception(()=>book.Confirm(f.Owner,Draft(),BusinessFixture.Phrase,Guid.NewGuid().ToString(),cancel.Token));Assert.True(reached);Assert.IsType(failure=="cancel"?typeof(OperationCanceledException):failure=="lock"?typeof(UnauthorizedAccessException):typeof(IOException),error);Assert.Equal(watermark,Count(f,"SELECT Watermark FROM Business;"));Assert.Equal(0,Count(f,"SELECT count(*) FROM BusinessProfileVersions;"));Assert.Equal(0,Count(f,"SELECT count(*) FROM Audit WHERE Action='Confirm private business profile';"));Assert.Equal("Fictional Beauty",TestStore.Scalar(f.Store.DatabasePath,"SELECT DisplayName FROM Business;"));
    }
    [Fact]
    public void CompetingReviewWinsBeforeWriterReservationWithoutOverwritingItsPrivateFacts()
    {
        using var f=new BusinessFixture();long watermark=Count(f,"SELECT Watermark FROM Business;");var other=new BusinessProfiles(new BusinessStore(f.Store.DatabasePath));BusinessProfileVersion? winner=null;int reservations=0;f.Store.BeforeTransactionReservation=()=>
        {
            if(++reservations!=2)return;f.Store.BeforeTransactionReservation=null;winner=other.Confirm(f.Owner,Draft() with{Profile=Draft().Profile with{DisplayName="Fictional competing profile"}},BusinessFixture.Phrase,Guid.NewGuid().ToString());
        };
        Assert.Throws<ArgumentException>(()=>new BusinessProfiles(f.Store).Confirm(f.Owner,Draft(),BusinessFixture.Phrase,Guid.NewGuid().ToString()));Assert.NotNull(winner);Assert.Equal(winner,Assert.Single(other.History(f.Owner)));Assert.Equal("Fictional competing profile",f.Store.BusinessName(f.Owner));Assert.Equal(watermark+1,Count(f,"SELECT Watermark FROM Business;"));
    }
    [Fact]
    public void SelectedEvidenceMustBeVerifiedAndRetainsTheExactManagedVersion()
    {
        using var f=new BusinessFixture();var attachments=new ManagedAttachments(f.Store);var file=attachments.Import(f.Owner,ManagedAttachmentTests.Pdf(f),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());string path=Path.Combine(f.Files.Folder,file.Version.RelativePath);byte[] bytes=File.ReadAllBytes(path);var draft=Draft() with{Profile=Draft().Profile with{Formation=new(BusinessSetupState.Confirmed,"Fictional formation review",ManagedEvidenceFile.From(file))}};var book=new BusinessProfiles(f.Store);long watermark=Count(f,"SELECT Watermark FROM Business;");File.AppendAllText(path,"Fictional changed source");Assert.Throws<InvalidDataException>(()=>book.Confirm(f.Owner,draft,BusinessFixture.Phrase,Guid.NewGuid().ToString()));Assert.Null(book.Current(f.Owner));Assert.Equal(watermark,Count(f,"SELECT Watermark FROM Business;"));File.WriteAllBytes(path,bytes);
        var saved=book.Confirm(f.Owner,draft,BusinessFixture.Phrase,Guid.NewGuid().ToString());attachments.Import(f.Owner,ManagedAttachmentTests.Pdf(f,"Fictional replacement setup evidence.pdf"),ManagedAttachmentTests.Draft with{AttachmentId=file.Version.AttachmentId,ExpectedVersion=1},Guid.NewGuid().ToString());Assert.Equal(ManagedEvidenceFile.From(file),book.Current(f.Owner)!.Profile.Formation.Evidence);Assert.Equal(saved,book.Current(f.Owner));
    }
}
