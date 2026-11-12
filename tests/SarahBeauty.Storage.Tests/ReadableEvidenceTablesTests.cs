using System.Text.Json;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableEvidenceTablesTests
{
    private const string Private="Fictional-private-attachment-label-purpose-reason";
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];

    [Fact]
    public void ReplacementAndReleasedLinksRetainExactOldVersionAndHashesWithoutPrivateInputsOrPostings()
    {
        using var f=new LedgerFixture();
        var files=new ManagedAttachments(f.Business.Store);
        var first=files.Import(f.Business.Owner,ManagedAttachmentTests.Pdf(f.Business,Private+".pdf"),new(Private,"Receipt",Private),Guid.NewGuid().ToString());
        var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Expense,2500) with{Name=Private});
        var book=new EvidenceLinks(f.Business.Store);
        var link=book.Link(f.Business.Owner,new(first.Version.Id,EvidenceTargetKind.MoneyDraft,draft.Id,draft.Revision,Private),Guid.NewGuid().ToString());
        var second=files.Import(f.Business.Owner,ManagedAttachmentTests.Pdf(f.Business,"Fictional replacement scan.pdf"),new(Private,"Receipt",Private,AttachmentId:first.Version.AttachmentId,ExpectedVersion:1),Guid.NewGuid().ToString());
        book.Release(f.Business.Owner,link.Link.Id,Private,Guid.NewGuid().ToString());
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableEvidenceTables.Build(source,new());
        Assert.Equal(3,tables.Length);
        string json=JsonSerializer.Serialize(tables);
        Assert.DoesNotContain(Private,json);
        Assert.DoesNotContain(f.Business.Files.Folder,json);
        var versions=tables.Single(t=>t.Name=="Managed evidence version references");
        Assert.Equal(2,versions.Rows.Count);
        var replacement=versions.Rows.Single(r=>r[0]==second.Version.Id);
        Assert.Equal(first.Version.Id,Cell(versions,replacement,"PreviousVersionId"));
        Assert.Equal(second.Version.Sha256,Cell(versions,replacement,"FileSha256"));
        var links=tables.Single(t=>t.Name=="Managed evidence links");
        var retained=Assert.Single(links.Rows);
        Assert.Equal(first.Version.Id,Cell(links,retained,"VersionId"));
        Assert.Equal(first.SnapshotHash,Cell(links,retained,"AttachmentSnapshotSha256"));
        Assert.Equal("MoneyDraft",Cell(links,retained,"TargetKind"));
        Assert.Equal("1",Cell(links,retained,"TargetRevision"));
        Assert.Equal("true",Cell(links,retained,"Released"));
        var releases=tables.Single(t=>t.Name=="Managed evidence link releases");
        Assert.Equal(link.SnapshotHash,Cell(releases,Assert.Single(releases.Rows),"LinkSnapshotSha256"));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void WithdrawnPhotoKeepsOnlyReferencesAndDoesNotRegainAccessThroughExport()
    {
        using var f=new BusinessFixture();
        var customers=new CustomerBook(f.Store);
        var customer=customers.Save(f.Owner,new(){FullName="Fictional readable photo subject",PhotoPermission="Granted",PermissionScope=Private});
        string input=Path.Combine(f.Files.Folder,Private+".png");
        byte[] bytes=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9ZlS8AAAAASUVORK5CYII=");
        File.WriteAllBytes(input,bytes);
        var files=new ManagedAttachments(f.Store);
        var saved=files.Import(f.Owner,input,new(Private,"Customer photo",Private,customer.Id,Private),Guid.NewGuid().ToString());
        customers.Save(f.Owner,customer with{PhotoPermission="Withdrawn"});
        Assert.Throws<ArgumentException>(()=>files.OpenRead(f.Owner,saved.Version.Id));
        using var source=new ReadableExportSnapshots(f.Store).Capture(f.Owner);
        var tables=ReadableEvidenceTables.Build(source,new());
        string json=JsonSerializer.Serialize(tables);
        Assert.DoesNotContain(Private,json);
        Assert.DoesNotContain(Convert.ToBase64String(bytes),json);
        var versions=tables.Single(t=>t.Name=="Managed evidence version references");
        var row=Assert.Single(versions.Rows);
        Assert.Equal(customer.Id,Cell(versions,row,"CustomerId"));
        Assert.Equal(saved.Version.PermissionId,Cell(versions,row,"PermissionId"));
        Assert.Equal(saved.Version.RelativePath,Cell(versions,row,"ManagedRelativePath"));
        Assert.Throws<ArgumentException>(()=>files.OpenRead(f.Owner,saved.Version.Id));
        Assert.Equal(bytes,File.ReadAllBytes(Path.Combine(f.Files.Folder,saved.Version.RelativePath)));
    }
}
