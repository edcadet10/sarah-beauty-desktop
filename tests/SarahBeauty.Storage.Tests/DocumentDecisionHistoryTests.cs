using System.Text.Json;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class DocumentDecisionHistoryTests
{
    [Fact]
    public void ExactSourceAndEvidenceSurviveReopenWithoutRequiringTheOriginalFile()
    {
        using var f=new BusinessFixture();var file=new ManagedAttachments(f.Store).Import(f.Owner,ManagedAttachmentTests.Pdf(f),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());var (version,value)=Insert(f,ManagedEvidenceFile.From(file));
        long watermark=Watermark(f);File.Delete(Path.Combine(f.Files.Folder,file.Version.RelativePath));var reopened=new BusinessStore(f.Store.DatabasePath);var history=new DocumentDecisions(reopened).History(f.Owner,version.Id);
        Assert.Equal(value,Assert.Single(history));Assert.Equal(file.SnapshotHash,history[0].Evidence!.MetadataHash);Assert.Equal(version.Hash,Assert.Single(new QuoteBook(reopened).Versions(f.Owner)).Hash);Assert.Equal(watermark,Watermark(f));
        Assert.Throws<NotSupportedException>(()=>((IList<DocumentDecision>)history).Clear());
        Assert.Equal(value,f.Store.Read(db=>DocumentDecisionQueries.Prior(db,f.Owner.BusinessId,value.RequestId,DocumentDecisionCodec.Payload(DocumentDecisionCodec.Draft(value)))));
        Assert.Throws<ArgumentException>(()=>f.Store.Read(db=>DocumentDecisionQueries.Prior(db,f.Owner.BusinessId,value.RequestId,new string('0',64))));
    }
    [Theory]
    [InlineData("DROP TRIGGER DocumentDecisions_NoUpdate; UPDATE DocumentDecisions SET SnapshotJson=replace(SnapshotJson,'PRIVATE','CHANGED');")]
    [InlineData("DROP TRIGGER DocumentDecisions_NoUpdate; UPDATE DocumentDecisions SET PayloadHash=printf('%064d',0);")]
    [InlineData("UPDATE DocumentVersions SET Status='Issued';")]
    public void ChangedPayloadAndContradictoryCurrentStatusCannotBeReportedAsValidHistory(string corruption)
    {
        using var f=new BusinessFixture();Insert(f);f.Files.Execute(corruption);Assert.Throws<InvalidDataException>(()=>new DocumentDecisions(f.Store).History(f.Owner));
    }
    [Fact]
    public void ForeignOrLockedSessionsCannotReadPrivateDecisionReasons()
    {
        using var f=new BusinessFixture();using var foreign=new BusinessFixture();Insert(f);var book=new DocumentDecisions(f.Store);
        Assert.Throws<UnauthorizedAccessException>(()=>book.History(foreign.Owner));f.Owner.Lock();Assert.Throws<UnauthorizedAccessException>(()=>book.History(f.Owner));
    }
    [Fact]
    public void ARehashedDecisionCannotSubstituteItsEvidenceMetadata()
    {
        using var f=new BusinessFixture();var file=new ManagedAttachments(f.Store).Import(f.Owner,ManagedAttachmentTests.Pdf(f),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());var (_,value)=Insert(f,ManagedEvidenceFile.From(file));
        var changed=value with{Evidence=value.Evidence! with{MetadataHash=new string('A',64)}};string json=JsonSerializer.Serialize(changed);f.Files.Execute("DROP TRIGGER DocumentDecisions_NoUpdate;");
        f.Store.Transaction(db=>db.Execute("UPDATE DocumentDecisions SET SnapshotJson=$json,SnapshotHash=$hash,PayloadHash=$payload;",("$json",json),("$hash",DocumentDecisionCodec.Hash(json)),("$payload",DocumentDecisionCodec.Payload(DocumentDecisionCodec.Draft(changed)))));
        Assert.Throws<InvalidDataException>(()=>new DocumentDecisions(f.Store).History(f.Owner));
    }
    internal static (DocumentVersion Version,DocumentDecision Decision) Insert(BusinessFixture f,ManagedEvidenceFile? evidence=null)
    {
        var quotes=new QuoteBook(f.Store);var draft=quotes.SaveDraft(f.Owner,quotes.FromBooking(f.Owner,QuoteTests.Booking(f)));var version=quotes.Issue(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        var value=new DocumentDecision(1,Guid.NewGuid().ToString(),f.Owner.BusinessId,version.DocumentId,version.Id,Guid.NewGuid().ToString(),DocumentDecisionAction.DeclineQuote,version.Snapshot.IssueDate,"Issued","Declined",version.Hash,"PRIVATE fictional reviewed decline",evidence,f.Owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));
        string json=JsonSerializer.Serialize(value);f.Store.Transaction(db=>
        {
            db.Execute("INSERT INTO DocumentDecisions(Id,BusinessId,DocumentId,VersionId,RequestId,PayloadHash,SnapshotJson,SnapshotHash,EvidenceVersionId,OwnerId,RecordedAtUtc) VALUES($id,$business,$document,$version,$request,$payload,$json,$hash,$evidence,$owner,$at);",("$id",value.Id),("$business",value.BusinessId),("$document",value.DocumentId),("$version",value.VersionId),("$request",value.RequestId),("$payload",DocumentDecisionCodec.Payload(DocumentDecisionCodec.Draft(value))),("$json",json),("$hash",DocumentDecisionCodec.Hash(json)),("$evidence",evidence?.VersionId),("$owner",value.OwnerId),("$at",value.RecordedAtUtc));
            db.Execute("UPDATE DocumentVersions SET Status='Declined' WHERE Id=$version;",("$version",version.Id));return true;
        });return(version,value);
    }
    private static long Watermark(BusinessFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));
}
