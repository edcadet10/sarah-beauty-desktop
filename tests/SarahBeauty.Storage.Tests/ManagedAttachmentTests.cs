using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PdfSharp.Pdf;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class ManagedAttachmentTests
{
    internal static string Pdf(BusinessFixture f,string name="Fictional evidence.pdf")
    {
        string path=Path.Combine(f.Files.Folder,name);using var document=new PdfDocument();document.AddPage();document.Info.Title="Fictional evidence fixture";document.Save(path);return path;
    }
    internal static AttachmentImport Draft=>new("Fictional supplier receipt","Receipt","Fictional imported source evidence");
    private static byte[] Read(ManagedAttachments book,BusinessFixture f,string id){using var stream=book.OpenRead(f.Owner,id);using var copy=new MemoryStream();stream.CopyTo(copy);return copy.ToArray();}

    [Fact]
    public void CopiedVersionsSurviveRemovedOriginalsAndRetriesCannotOverwriteHistory()
    {
        using var f=new BusinessFixture();string source=Pdf(f),request=Guid.NewGuid().ToString();byte[] original=File.ReadAllBytes(source);var book=new ManagedAttachments(f.Store);
        var first=book.Import(f.Owner,source,Draft,request);Assert.Equal(first,book.Import(f.Owner,source,Draft,request.ToUpperInvariant()));
        Assert.Throws<ArgumentException>(()=>book.Import(f.Owner,source,Draft with{Name="Fictional conflicting retry"},request));
        File.Delete(source);Assert.Equal(original,Read(book,f,first.Version.Id));Assert.Equal(Convert.ToHexString(SHA256.HashData(original)),first.Version.Sha256);
        string replacement=Pdf(f,"Fictional replacement.pdf");var next=Draft with{AttachmentId=first.Version.AttachmentId,ExpectedVersion=1,Reason="Fictional corrected scan"};
        var second=book.Import(f.Owner,replacement,next,Guid.NewGuid().ToString());Assert.Equal(2,second.Version.Number);Assert.Equal(first.Version.Id,second.Version.PreviousVersionId);
        Assert.NotEqual(first.Version.RelativePath,second.Version.RelativePath);Assert.Equal(original,Read(book,f,first.Version.Id));
        Assert.Throws<InvalidOperationException>(()=>book.Import(f.Owner,replacement,next,Guid.NewGuid().ToString()));
        Assert.Equal(2,new ManagedAttachments(new BusinessStore(f.Files.DatabasePath)).History(f.Owner).Count);
        Assert.Equal(2L,TestStore.Scalar(f.Files.DatabasePath,"SELECT COUNT(*) FROM Audit WHERE Action='Import managed attachment version';"));
        Assert.DoesNotContain(f.Files.Folder,JsonSerializer.Serialize(book.History(f.Owner)));
    }

    [Theory]
    [InlineData("Copy block")]
    [InlineData("File published")]
    [InlineData("Metadata written")]
    public void InterruptedImportsLeaveNoPublishedRecordAndRetryOnlyOnce(string point)
    {
        using var f=new BusinessFixture();string source=Pdf(f),request=Guid.NewGuid().ToString();long watermark=(long)TestStore.Scalar(f.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        var book=new ManagedAttachments(f.Store){FailurePoint=stage=>{if(stage==point)throw new IOException("Fictional storage interruption");}};
        Assert.Throws<IOException>(()=>book.Import(f.Owner,source,Draft,request));Assert.Empty(book.History(f.Owner));
        Assert.Equal(watermark,TestStore.Scalar(f.Files.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Empty(Directory.EnumerateFiles(Path.Combine(f.Files.Folder,"Attachments")));
        book.FailurePoint=null;var saved=book.Import(f.Owner,source,Draft,request);Assert.Equal(saved,book.Import(f.Owner,source,Draft,request));Assert.Single(book.History(f.Owner));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedFileOrMetadataCannotPassVerifiedRead(bool metadata)
    {
        using var f=new BusinessFixture();var book=new ManagedAttachments(f.Store);var saved=book.Import(f.Owner,Pdf(f),Draft,Guid.NewGuid().ToString());
        Assert.Throws<SqliteException>(()=>f.Files.Execute("UPDATE AttachmentVersions SET SnapshotJson='{}';"));Assert.Throws<SqliteException>(()=>f.Files.Execute("DELETE FROM AttachmentVersions;"));
        if(metadata)f.Files.Execute("DROP TRIGGER AttachmentVersions_NoUpdate; UPDATE AttachmentVersions SET SnapshotJson=json_set(SnapshotJson,'$.Name','Changed fixture');");
        else File.AppendAllText(Path.Combine(f.Files.Folder,saved.Version.RelativePath),"Fictional changed bytes");
        Assert.Throws<InvalidDataException>(()=>book.OpenRead(f.Owner,saved.Version.Id));
        if(metadata)Assert.Throws<InvalidDataException>(()=>book.History(f.Owner));
    }

    [Fact]
    public void PhotosRequireCurrentExactPermissionAndWithdrawalBlocksReadingOldCopies()
    {
        using var f=new BusinessFixture();var customers=new CustomerBook(f.Store);var customer=customers.Save(f.Owner,new(){FullName="Fictional photo subject"});
        string source=Path.Combine(f.Files.Folder,"Fictional private photo.png");File.WriteAllBytes(source,Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9ZlS8AAAAASUVORK5CYII="));
        var book=new ManagedAttachments(f.Store);var draft=new AttachmentImport("Fictional appointment photo","Customer photo","Fictional permission-backed import",customer.Id,"Internal appointment reference");
        Assert.Throws<ArgumentException>(()=>book.Import(f.Owner,source,draft,Guid.NewGuid().ToString()));Assert.Empty(book.History(f.Owner));
        customer=customers.Save(f.Owner,customer with{PhotoPermission="Granted",PermissionScope=draft.PermissionUse});
        Assert.Throws<ArgumentException>(()=>book.Import(f.Owner,source,draft with{PermissionUse="Public marketing"},Guid.NewGuid().ToString()));
        Assert.Throws<ArgumentException>(()=>book.Import(f.Owner,Pdf(f),draft,Guid.NewGuid().ToString()));
        var saved=book.Import(f.Owner,source,draft,Guid.NewGuid().ToString());Assert.NotEmpty(saved.Version.PermissionId);Assert.Equal(File.ReadAllBytes(source),Read(book,f,saved.Version.Id));
        customers.Save(f.Owner,customer with{PhotoPermission="Withdrawn"});Assert.Throws<ArgumentException>(()=>book.OpenRead(f.Owner,saved.Version.Id));
        Assert.Equal(saved,book.Get(f.Owner,saved.Version.Id));Assert.Single(book.History(f.Owner));Assert.Equal(3,customers.Permissions(f.Owner,customer.Id).Count);
    }

    [Fact]
    public void TypeSizeCancellationAndBusinessBoundariesRejectBeforePublishing()
    {
        using var f=new BusinessFixture();using var foreign=new BusinessFixture();var book=new ManagedAttachments(f.Store);string source=Pdf(f);
        foreach(string extension in new[]{".exe",".svg",".jpg"})
        {
            string wrong=Path.Combine(f.Files.Folder,"Fictional wrong type"+extension);File.Copy(source,wrong);Assert.Throws<ArgumentException>(()=>book.Import(f.Owner,wrong,Draft,Guid.NewGuid().ToString()));
        }
        string oversized=Path.Combine(f.Files.Folder,"Fictional oversized.pdf");using(var file=File.Create(oversized))file.SetLength(AttachmentCopy.MaximumBytes+1);
        Assert.Throws<ArgumentException>(()=>book.Import(f.Owner,oversized,Draft,Guid.NewGuid().ToString()));
        using var cancel=new CancellationTokenSource();book.FailurePoint=_=>cancel.Cancel();Assert.Throws<OperationCanceledException>(()=>book.Import(f.Owner,source,Draft,Guid.NewGuid().ToString(),cancel.Token));
        book.FailurePoint=null;Assert.Throws<UnauthorizedAccessException>(()=>book.Import(foreign.Owner,source,Draft,Guid.NewGuid().ToString()));
        Assert.Empty(book.History(f.Owner));Assert.Empty(Directory.EnumerateFiles(Path.Combine(f.Files.Folder,"Attachments")));
        var saved=book.Import(f.Owner,source,Draft,Guid.NewGuid().ToString());Assert.Throws<UnauthorizedAccessException>(()=>book.OpenRead(foreign.Owner,saved.Version.Id));
        File.Delete(Path.Combine(f.Files.Folder,saved.Version.RelativePath));Assert.Throws<InvalidDataException>(()=>book.OpenRead(f.Owner,saved.Version.Id));
    }

    [Theory]
    [InlineData("../outside.pdf")]
    [InlineData("Attachments/../../outside.pdf")]
    [InlineData("Attachments/source.pdf:stream")]
    [InlineData("C:/outside.pdf")]
    public void ARehashedPathOutsideGeneratedManagedNamesCannotBeRead(string path)
    {
        using var f=new BusinessFixture();var book=new ManagedAttachments(f.Store);var saved=book.Import(f.Owner,Pdf(f),Draft,Guid.NewGuid().ToString());
        string json=JsonSerializer.Serialize(saved.Version with{RelativePath=path});f.Files.Execute("DROP TRIGGER AttachmentVersions_NoUpdate;");
        f.Store.Transaction(db=>db.Execute("UPDATE AttachmentVersions SET SnapshotJson=$json,SnapshotHash=$hash;",("$json",json),("$hash",AttachmentQueries.Hash(json))));
        Assert.Throws<InvalidDataException>(()=>book.OpenRead(f.Owner,saved.Version.Id));
    }

    [Fact]
    public void VersionTwentyEightUpgradeRetainsExistingBusinessAndVerifiesItsSnapshot()
    {
        using var f=new BusinessFixture();var customer=new CustomerBook(f.Store).Save(f.Owner,new(){FullName="Fictional retained customer"});f.Files.PrepareBankingVersion(28);
        var store=new BusinessStore(f.Files.DatabasePath);Assert.Equal(BusinessSchema.Version,LocalDatabase.InitializeTestDatabaseAt(f.Files.DatabasePath).SchemaVersion);Assert.Single(f.Files.Snapshots());
        Assert.Equal(customer,new CustomerBook(store).Search(f.Owner).Single());var book=new ManagedAttachments(store);var saved=book.Import(f.Owner,Pdf(f),Draft,Guid.NewGuid().ToString());
        Assert.NotEmpty(Read(book,f,saved.Version.Id));Assert.Equal("ok",TestStore.Scalar(f.Files.DatabasePath,"PRAGMA integrity_check;"));
    }
}
