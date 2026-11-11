using System.Text.Json;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class FinancialEvidenceTests
{
    private static StoredAttachment Import(LedgerFixture f)=>new ManagedAttachments(f.Business.Store).Import(f.Business.Owner,ManagedAttachmentTests.Pdf(f.Business),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());
    [Fact]
    public void DraftAndApprovalKeepTheSelectedFileVersionWhileNotesAndMoneyRemainSeparate()
    {
        using var f=new LedgerFixture();var file=Import(f);var evidence=ManagedEvidenceFile.From(file);
        var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Expense,2500) with{EvidenceFile=evidence,EvidenceId="Fictional approval decision notes"});
        Assert.Empty(f.Book.Posted(f.Business.Owner));Assert.Equal(evidence,draft.EvidenceFile);
        new ManagedAttachments(f.Business.Store).Import(f.Business.Owner,ManagedAttachmentTests.Pdf(f.Business,"Fictional replacement source.pdf"),ManagedAttachmentTests.Draft with{AttachmentId=file.Version.AttachmentId,ExpectedVersion=1},Guid.NewGuid().ToString());
        var posted=f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);Assert.Equal(evidence,posted.Source.EvidenceFile);Assert.Equal("Fictional approval decision notes",posted.Source.EvidenceId);
        Assert.Equal(2500,f.Balance("5010"));Assert.Equal(-2500,f.Balance("1010"));Assert.Single(f.Book.Posted(f.Business.Owner));
        var persisted=new FinanceBook(new BusinessStore(f.Business.Files.DatabasePath)).Posted(f.Business.Owner).Single();Assert.Equal(evidence,persisted.Source.EvidenceFile);
        Assert.Equal(file,new FinancialEvidence(f.Business.Store).Read(f.Business.Owner,evidence));
    }

    [Fact]
    public void ForgedCapturedFactsForeignFilesAndPrivatePhotosCannotBecomeFinancialEvidence()
    {
        using var f=new LedgerFixture();using var foreign=new LedgerFixture();var file=Import(f);var evidence=ManagedEvidenceFile.From(file);var draft=f.Draft(FinancialKind.Expense,1000) with{EvidenceFile=evidence};
        foreach(var changed in new[]{evidence with{Name="Fictional forged name"},evidence with{MetadataHash="BAD"},evidence with{Number=99},evidence with{Sha256="BAD"}})
        {
            Assert.Throws<InvalidDataException>(()=>f.Book.SaveDraft(f.Business.Owner,draft with{EvidenceFile=changed}));Assert.Throws<InvalidDataException>(()=>f.Book.Preview(f.Business.Owner,draft with{EvidenceFile=changed}));
        }
        var other=ManagedEvidenceFile.From(Import(foreign));Assert.Throws<ArgumentException>(()=>f.Book.SaveDraft(f.Business.Owner,draft with{EvidenceFile=other}));
        var customers=new SarahBeauty.Business.Customers.CustomerBook(f.Business.Store);var customer=customers.Search(f.Business.Owner).Single();customer=customers.Save(f.Business.Owner,customer with{PhotoPermission="Granted",PermissionScope="Fictional internal reference"});
        string photo=Path.Combine(f.Business.Files.Folder,"Fictional private.jpg");File.WriteAllBytes(photo,[255,216,255,224,0,16,74,70,73,70,0]);
        var privateFile=new ManagedAttachments(f.Business.Store).Import(f.Business.Owner,photo,new("Fictional photo","Customer photo","Fictional scoped image",customer.Id,customer.PermissionScope),Guid.NewGuid().ToString());
        Assert.Throws<ArgumentException>(()=>f.Book.SaveDraft(f.Business.Owner,draft with{EvidenceFile=ManagedEvidenceFile.From(privateFile)}));Assert.Empty(f.Book.Drafts(f.Business.Owner));Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedOrMissingContentsBlockPreviewAndApprovalWithoutPosting(bool missing)
    {
        using var f=new LedgerFixture();var file=Import(f);var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Expense,1000) with{EvidenceFile=ManagedEvidenceFile.From(file)});
        string path=Path.Combine(f.Business.Files.Folder,file.Version.RelativePath);if(missing)File.Delete(path);else File.AppendAllText(path,"Fictional corruption");
        long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        Assert.Throws<InvalidDataException>(()=>f.Book.Preview(f.Business.Owner,draft));Assert.Throws<InvalidDataException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Empty(f.Book.Posted(f.Business.Owner));Assert.Equal(0,f.Balance("1010"));Assert.Equal(watermark,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Equal("Draft",f.Book.Drafts(f.Business.Owner).Single().Status);
    }

    [Fact]
    public void RehashedMetadataCannotReplaceTheOriginalReviewedReference()
    {
        using var f=new LedgerFixture();var file=Import(f);var evidence=ManagedEvidenceFile.From(file);var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Expense,1000) with{EvidenceFile=evidence});
        string json=JsonSerializer.Serialize(file.Version with{Name="Fictional changed source"});f.Business.Files.Execute("DROP TRIGGER AttachmentVersions_NoUpdate;");
        f.Business.Store.Transaction(db=>db.Execute("UPDATE AttachmentVersions SET SnapshotJson=$json,SnapshotHash=$hash;",("$json",json),("$hash",AttachmentQueries.Hash(json))));
        Assert.Throws<InvalidDataException>(()=>new FinancialEvidence(f.Business.Store).Read(f.Business.Owner,evidence));Assert.Throws<InvalidDataException>(()=>f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void ACorrectionDoesNotRequireReopeningMissingOriginalEvidenceOrRewriteItsSource()
    {
        using var f=new LedgerFixture();var file=Import(f);var evidence=ManagedEvidenceFile.From(file);var original=f.Post(f.Draft(FinancialKind.Expense,1000) with{EvidenceFile=evidence});File.Delete(Path.Combine(f.Business.Files.Folder,file.Version.RelativePath));
        var reversal=f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=original.Id,Date=f.Date.AddDays(1),EvidenceId="Fictional separately reviewed correction"});
        Assert.Null(reversal.Source.EvidenceFile);Assert.Equal(evidence,f.Book.Posted(f.Business.Owner).Single(p=>p.Id==original.Id).Source.EvidenceFile);Assert.Equal(0,f.Balance("1010"));Assert.Equal(0,f.Balance("5010"));
    }

    [Fact]
    public void LegacyTextReferencesRemainTextAndAbsentFilesDoNotChangeTheirSerializedShape()
    {
        using var f=new LedgerFixture();var draft=f.Draft(FinancialKind.Expense,1000) with{EvidenceId="Fictional older text reference"};string json=JsonSerializer.Serialize(draft);
        Assert.DoesNotContain("EvidenceFile",json);var older=JsonSerializer.Deserialize<FinancialDraft>(json)!;Assert.Null(older.EvidenceFile);
        var posted=f.Post(older);Assert.Null(posted.Source.EvidenceFile);Assert.Equal(older.EvidenceId,posted.Source.EvidenceId);Assert.Equal(1000,f.Balance("5010"));
    }
}
