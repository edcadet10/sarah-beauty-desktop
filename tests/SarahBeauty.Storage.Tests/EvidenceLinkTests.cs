using System.Text.Json;
using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class EvidenceLinkTests
{
    private static StoredAttachment File(BusinessFixture f)=>new ManagedAttachments(f.Store).Import(f.Owner,ManagedAttachmentTests.Pdf(f),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());
    private static EvidenceLinkDraft Draft(StoredAttachment file,EvidenceTarget target)=>new(file.Version.Id,target.Kind,target.Id,target.Revision,"Fictional supporting evidence review");
    private static LinkedEvidence CustomerLink(BusinessFixture f,EvidenceLinks book,StoredAttachment file)
    {
        var customer=new CustomerBook(f.Store).Save(f.Owner,new(){FullName="Fictional evidence customer"});return book.Link(f.Owner,Draft(file,new(EvidenceTargetKind.Customer,customer.Id,customer.FullName,customer.Revision,"Customer",false)),Guid.NewGuid().ToString());
    }

    [Fact]
    public void LinksPinAReviewedFileAndTargetRevisionWithoutChangingFinancialPostings()
    {
        using var f=new LedgerFixture();var file=File(f.Business);var book=new EvidenceLinks(f.Business.Store);var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Expense,2500));
        var target=new EvidenceTargets(f.Business.Store).List(f.Business.Owner,EvidenceTargetKind.MoneyDraft).Single();string request=Guid.NewGuid().ToString();var input=Draft(file,target);
        var link=book.Link(f.Business.Owner,input,request);Assert.Equal(link,book.Link(f.Business.Owner,input,request.ToUpperInvariant()));Assert.Empty(f.Book.Posted(f.Business.Owner));
        var posted=f.Book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);Assert.Equal(-2500,f.Balance("1010"));
        var files=new ManagedAttachments(f.Business.Store);var replacement=files.Import(f.Business.Owner,ManagedAttachmentTests.Pdf(f.Business,"Fictional newer scan.pdf"),ManagedAttachmentTests.Draft with{AttachmentId=file.Version.AttachmentId,ExpectedVersion=1},Guid.NewGuid().ToString());
        var retained=Assert.Single(book.History(f.Business.Owner));Assert.Equal(file.Version.Id,retained.Link.AttachmentVersionId);Assert.NotEqual(replacement.Version.Id,retained.Link.AttachmentVersionId);Assert.Equal(1,retained.Link.Target.Revision);
        using(var stream=book.OpenRead(f.Business.Owner,link.Link.Id))Assert.Equal(file.Version.ByteLength,stream.Length);
        var movement=new EvidenceTargets(f.Business.Store).List(f.Business.Owner,EvidenceTargetKind.PostedMoney).Single();Assert.Equal(posted.Id,movement.Id);
        book.Link(f.Business.Owner,Draft(file,movement),Guid.NewGuid().ToString());Assert.Single(f.Book.Posted(f.Business.Owner));Assert.Equal(-2500,f.Balance("1010"));
        var released=book.Release(f.Business.Owner,link.Link.Id,"Fictional redundant draft association",Guid.NewGuid().ToString());Assert.NotNull(released.Release);
        Assert.NotNull(book.Link(f.Business.Owner,input,request).Release);Assert.Single(book.History(f.Business.Owner),l=>l.Release is null);Assert.Equal(2500,f.Balance("5010"));
    }

    [Fact]
    public void TargetsRequireTheReviewedTypeBusinessAndRevision()
    {
        using var f=new BusinessFixture();using var foreign=new BusinessFixture();var file=File(f);var book=new EvidenceLinks(f.Store);var customers=new CustomerBook(f.Store);
        var customer=customers.Save(f.Owner,new(){FullName="Fictional target"});var input=new EvidenceLinkDraft(file.Version.Id,EvidenceTargetKind.Customer,customer.Id,customer.Revision,"Fictional evidence");
        customers.Save(f.Owner,customer with{FullName="Fictional renamed target"});Assert.Throws<InvalidOperationException>(()=>book.Link(f.Owner,input,Guid.NewGuid().ToString()));
        Assert.Throws<ArgumentException>(()=>book.Link(f.Owner,input with{TargetKind=EvidenceTargetKind.Booking},Guid.NewGuid().ToString()));
        Assert.Throws<ArgumentException>(()=>book.Link(f.Owner,input with{TargetKind=(EvidenceTargetKind)999},Guid.NewGuid().ToString()));
        var outsider=new CustomerBook(foreign.Store).Save(foreign.Owner,new(){FullName="Fictional other business"});Assert.Throws<ArgumentException>(()=>book.Link(f.Owner,input with{TargetId=outsider.Id},Guid.NewGuid().ToString()));
        Assert.Throws<UnauthorizedAccessException>(()=>book.Link(foreign.Owner,input,Guid.NewGuid().ToString()));Assert.Throws<UnauthorizedAccessException>(()=>new EvidenceTargets(f.Store).List(foreign.Owner,EvidenceTargetKind.Customer));
        Assert.Empty(book.History(f.Owner));
    }

    [Fact]
    public void PhotoLinksStayWithTheirOwnCustomerAndWithdrawalStillAllowsHistoricalCorrection()
    {
        using var f=new BusinessFixture();var customers=new CustomerBook(f.Store);var customer=customers.Save(f.Owner,new(){FullName="Fictional subject",PhotoPermission="Granted",PermissionScope="Internal reference"});
        var other=customers.Save(f.Owner,new(){FullName="Fictional other customer"});string source=Path.Combine(f.Files.Folder,"Fictional subject.jpg");System.IO.File.WriteAllBytes(source,[255,216,255,224,0,16,74,70,73,70,0]);
        var file=new ManagedAttachments(f.Store).Import(f.Owner,source,new("Fictional private image","Customer photo","Fictional imported image",customer.Id,customer.PermissionScope),Guid.NewGuid().ToString());
        var book=new EvidenceLinks(f.Store);var input=new EvidenceLinkDraft(file.Version.Id,EvidenceTargetKind.Customer,customer.Id,customer.Revision,"Fictional internal customer reference");
        Assert.Throws<ArgumentException>(()=>book.Link(f.Owner,input with{TargetId=other.Id,ExpectedTargetRevision=other.Revision},Guid.NewGuid().ToString()));
        var linked=book.Link(f.Owner,input,Guid.NewGuid().ToString());customers.Save(f.Owner,customer with{PhotoPermission="Withdrawn"});
        Assert.Throws<ArgumentException>(()=>book.OpenRead(f.Owner,linked.Link.Id));book.Release(f.Owner,linked.Link.Id,"Fictional discontinued use after withdrawal",Guid.NewGuid().ToString());
        Assert.NotNull(Assert.Single(book.History(f.Owner)).Release);Assert.Single(new ManagedAttachments(f.Store).History(f.Owner));
    }

    [Fact]
    public void InterruptedLinkAndReleaseDecisionsRollBackAndRetryWithoutDuplication()
    {
        using var f=new BusinessFixture();var file=File(f);var book=new EvidenceLinks(f.Store);var customer=new CustomerBook(f.Store).Save(f.Owner,new(){FullName="Fictional interrupted association"});
        var input=new EvidenceLinkDraft(file.Version.Id,EvidenceTargetKind.Customer,customer.Id,customer.Revision,"Fictional reviewed association");string request=Guid.NewGuid().ToString();
        long watermark=(long)TestStore.Scalar(f.Files.DatabasePath,"SELECT Watermark FROM Business;")!;book.FailurePoint=_=>throw new IOException("Fictional interrupted decision");
        Assert.Throws<IOException>(()=>book.Link(f.Owner,input,request));Assert.Empty(book.History(f.Owner));Assert.Equal(watermark,TestStore.Scalar(f.Files.DatabasePath,"SELECT Watermark FROM Business;"));
        book.FailurePoint=null;var link=book.Link(f.Owner,input,request);Assert.Throws<ArgumentException>(()=>book.Link(f.Owner,input,Guid.NewGuid().ToString()));
        Assert.Throws<ArgumentException>(()=>book.Link(f.Owner,input with{Purpose="Changed retry"},request));string release=Guid.NewGuid().ToString();book.FailurePoint=_=>throw new IOException("Fictional interrupted release");
        Assert.Throws<IOException>(()=>book.Release(f.Owner,link.Link.Id,"Fictional correction",release));Assert.Null(Assert.Single(book.History(f.Owner)).Release);
        book.FailurePoint=null;var result=book.Release(f.Owner,link.Link.Id,"Fictional correction",release);Assert.Equal(result,book.Release(f.Owner,link.Link.Id,"Fictional correction",release.ToUpperInvariant()));
        Assert.Throws<ArgumentException>(()=>book.Release(f.Owner,link.Link.Id,"Different reason",release));Assert.Single(book.History(f.Owner));
        Assert.Equal(result.Release!.Id,new EvidenceLinks(new BusinessStore(f.Files.DatabasePath)).History(f.Owner).Single().Release!.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedLinkOrReleaseSnapshotsCannotEnterHistoryOrContentReads(bool release)
    {
        using var f=new BusinessFixture();var file=File(f);var book=new EvidenceLinks(f.Store);var linked=CustomerLink(f,book,file);
        Assert.Throws<SqliteException>(()=>f.Files.Execute("UPDATE AttachmentLinks SET SnapshotJson='{}';"));Assert.Throws<SqliteException>(()=>f.Files.Execute("DELETE FROM AttachmentLinks;"));
        if(release)
        {
            book.Release(f.Owner,linked.Link.Id,"Fictional correction",Guid.NewGuid().ToString());Assert.Throws<SqliteException>(()=>f.Files.Execute("DELETE FROM AttachmentLinkReleases;"));
            f.Files.Execute("DROP TRIGGER AttachmentLinkReleases_NoUpdate; UPDATE AttachmentLinkReleases SET SnapshotJson='{}';");
        }
        else f.Files.Execute("DROP TRIGGER AttachmentLinks_NoUpdate; UPDATE AttachmentLinks SET SnapshotJson='{}';");
        Assert.Throws<InvalidDataException>(()=>book.History(f.Owner));Assert.Throws<InvalidDataException>(()=>book.OpenRead(f.Owner,linked.Link.Id));
    }

    [Fact]
    public void LinkedReadChecksTheOriginalAttachmentMetadataHashEvenIfItsRowWasRehashed()
    {
        using var f=new BusinessFixture();var file=File(f);var book=new EvidenceLinks(f.Store);var link=CustomerLink(f,book,file);
        string json=JsonSerializer.Serialize(file.Version with{Name="Fictional altered retained name"});f.Files.Execute("DROP TRIGGER AttachmentVersions_NoUpdate;");
        f.Store.Transaction(db=>db.Execute("UPDATE AttachmentVersions SET SnapshotJson=$json,SnapshotHash=$hash;",("$json",json),("$hash",AttachmentQueries.Hash(json))));
        Assert.Equal("Fictional altered retained name",new ManagedAttachments(f.Store).Get(f.Owner,file.Version.Id).Version.Name);
        Assert.Throws<InvalidDataException>(()=>book.OpenRead(f.Owner,link.Link.Id));
    }

    [Fact]
    public void VersionTwentyNineUpgradeRetainsImportedFileVersionsAndAddsConstrainedLinks()
    {
        using var f=new BusinessFixture();var file=File(f);f.Files.PrepareBankingVersion(29);var store=new BusinessStore(f.Files.DatabasePath);var book=new EvidenceLinks(store);
        Assert.Single(f.Files.Snapshots());Assert.Equal(file,new ManagedAttachments(store).Get(f.Owner,file.Version.Id));
        var link=CustomerLink(f,book,file);using var stream=book.OpenRead(f.Owner,link.Link.Id);Assert.Equal(file.Version.ByteLength,stream.Length);
        Assert.Empty(new FinanceBook(store).Posted(f.Owner));Assert.Equal("ok",TestStore.Scalar(f.Files.DatabasePath,"PRAGMA integrity_check;"));
    }
}
