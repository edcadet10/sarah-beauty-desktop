using System.Security.Cryptography;
using System.Text.Json;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Recovery;

namespace SarahBeauty.Storage.Tests;

public sealed class ArchiveSourceTests
{
    private static StoredAttachment Imported(LedgerFixture f)=>new ManagedAttachments(f.Business.Store).Import(f.Business.Owner,ManagedAttachmentTests.Pdf(f.Business),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());
    private static void Clean(LedgerFixture f)=>Assert.Empty(Directory.Exists(Path.Combine(f.Business.Files.Folder,"RecoveryStaging"))?Directory.EnumerateFileSystemEntries(Path.Combine(f.Business.Files.Folder,"RecoveryStaging")):[]);

    [Fact]
    public void CapturedDatabaseAndAllFileVersionsAreIndependentVerifiedAndReadOnly()
    {
        using var f=new LedgerFixture();f.Post(f.Draft(FinancialKind.Receipt,10000));f.Post(f.Draft(FinancialKind.Expense,2500));var invoice=ApplicationTests.Invoice(f,10000);new DocumentArtifacts(f.Business.Store).Render(f.Business.Owner,invoice.Id);
        var first=Imported(f);var files=new ManagedAttachments(f.Business.Store);files.Import(f.Business.Owner,ManagedAttachmentTests.Pdf(f.Business,"Fictional replacement.pdf"),ManagedAttachmentTests.Draft with{AttachmentId=first.Version.AttachmentId,ExpectedVersion=1},Guid.NewGuid().ToString());
        var customers=new SarahBeauty.Business.Customers.CustomerBook(f.Business.Store);var customer=customers.Search(f.Business.Owner).Single();customer=customers.Save(f.Business.Owner,customer with{PhotoPermission="Granted",PermissionScope="Fictional internal reference"});
        string photo=Path.Combine(f.Business.Files.Folder,"fictional-private.jpg");File.WriteAllBytes(photo,[255,216,255,224,0,16,74,70,73,70,0]);
        var image=files.Import(f.Business.Owner,photo,new("Fictional private reference","Customer photo","Fictional permitted image",customer.Id,customer.PermissionScope),Guid.NewGuid().ToString());customers.Save(f.Business.Owner,customer with{PhotoPermission="Withdrawn"});
        Assert.Throws<ArgumentException>(()=>files.OpenRead(f.Business.Owner,image.Version.Id));
        File.WriteAllText(Path.Combine(f.Business.Files.Folder,"private-unreferenced.txt"),"Fictional external file is not a managed reference");
        long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        using(var captured=new ArchiveSources(f.Business.Store).Capture(f.Business.Owner))
        {
            var manifest=captured.Manifest;Assert.Equal("TEST",manifest.Mode);Assert.Equal(f.Business.Store.StoreId,manifest.StoreId);Assert.Equal(watermark,manifest.Watermark);Assert.Equal(5,manifest.Files.Length);
            Assert.Equal(2,manifest.Facts.MovementCount);Assert.Equal(4,manifest.Facts.JournalLineCount);Assert.Equal("12500",manifest.Facts.TotalDebits);Assert.Equal("12500",manifest.Facts.TotalCredits);
            Assert.Equal(3,manifest.Files.Count(p=>p.Kind=="Attachment"));Assert.Single(manifest.Files,p=>p.Kind=="PDF");Assert.Single(manifest.Files,p=>p.Kind=="Database");
            foreach(var file in manifest.Files){Assert.Equal(file.ByteLength,new FileInfo(captured.FilePath(file.RelativePath)).Length);Assert.Equal(file.Sha256,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(captured.FilePath(file.RelativePath)))));}
            File.Delete(Path.Combine(f.Business.Files.Folder,first.Version.RelativePath));Assert.Equal(first.Version.Sha256,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(captured.FilePath(first.Version.RelativePath)))));
            Assert.DoesNotContain(manifest.Files,p=>p.RelativePath.Contains("unreferenced"));Assert.Equal(watermark,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;"));
        }
        Clean(f);Assert.Equal(7500,f.Balance("1010"));
    }

    [Fact]
    public void CommittedWalSnapshotDoesNotMixLaterTransactionsOrNewFileVersions()
    {
        using var f=new LedgerFixture();f.Business.Files.Execute("PRAGMA journal_mode=WAL;");f.Post(f.Draft(FinancialKind.Receipt,10000));var file=Imported(f);long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        var sources=new ArchiveSources(f.Business.Store){FailurePoint=point=>
        {
            if(point!="Database captured")return;f.Post(f.Draft(FinancialKind.Expense,2500));new ManagedAttachments(f.Business.Store).Import(f.Business.Owner,ManagedAttachmentTests.Pdf(f.Business,"Fictional later file.pdf"),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());
        }};
        using(var captured=sources.Capture(f.Business.Owner))
        {
            Assert.Equal(watermark,captured.Manifest.Watermark);Assert.Equal(1,captured.Manifest.Facts.MovementCount);Assert.Equal("10000",captured.Manifest.Facts.TotalDebits);
            Assert.Equal(file.Version.Sha256,Assert.Single(captured.Manifest.Files,p=>p.Kind=="Attachment").Sha256);
            Assert.Equal(1L,TestStore.Scalar(captured.FilePath("sarahbeauty.db"),"SELECT COUNT(*) FROM Movements;"));Assert.Equal("delete",TestStore.Scalar(captured.FilePath("sarahbeauty.db"),"PRAGMA journal_mode;"));
        }
        Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);Assert.Equal(7500,f.Balance("1010"));Clean(f);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("changed")]
    [InlineData("metadata")]
    [InlineData("journal")]
    public void IncompleteOrInvalidSourcesDoNotProduceACapturedStore(string defect)
    {
        using var f=new LedgerFixture();var posted=f.Post(f.Draft(FinancialKind.Expense,1000));var file=Imported(f);string path=Path.Combine(f.Business.Files.Folder,file.Version.RelativePath);
        switch(defect)
        {
            case "missing":File.Delete(path);break;
            case "changed":File.AppendAllText(path,"Fictional changed bytes");break;
            case "metadata":f.Business.Files.Execute("DROP TRIGGER AttachmentVersions_NoUpdate; UPDATE AttachmentVersions SET SnapshotJson=json_set(SnapshotJson,'$.Name','Changed fixture');");break;
            case "journal":f.Business.Store.Transaction(db=>db.Execute("INSERT INTO JournalLines (Id,MovementId,AccountId,DebitCents,CreditCents) VALUES ($id,$movement,$account,1,0);",("$id",Guid.NewGuid().ToString()),("$movement",posted.Id),("$account",f.Accounts["5010"].Id)));break;
        }
        long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        if(defect=="missing")Assert.Throws<FileNotFoundException>(()=>new ArchiveSources(f.Business.Store).Capture(f.Business.Owner));
        else Assert.Throws<InvalidDataException>(()=>new ArchiveSources(f.Business.Store).Capture(f.Business.Owner));
        Clean(f);Assert.Equal(watermark,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Single(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void UnsafeRehashedPathsAndForeignSessionsCannotCopyUnmanagedFiles()
    {
        using var f=new LedgerFixture();using var foreign=new BusinessFixture();var file=Imported(f);string outside=Path.Combine(f.Business.Files.Folder,"outside.pdf");File.WriteAllText(outside,"Fictional unrelated content");
        string json=JsonSerializer.Serialize(file.Version with{RelativePath="../outside.pdf"});f.Business.Files.Execute("DROP TRIGGER AttachmentVersions_NoUpdate;");
        f.Business.Store.Transaction(db=>db.Execute("UPDATE AttachmentVersions SET SnapshotJson=$json,SnapshotHash=$hash;",("$json",json),("$hash",AttachmentQueries.Hash(json))));
        Assert.Throws<InvalidDataException>(()=>new ArchiveSources(f.Business.Store).Capture(f.Business.Owner));Clean(f);Assert.Equal("Fictional unrelated content",File.ReadAllText(outside));
        Assert.Throws<UnauthorizedAccessException>(()=>new ArchiveSources(f.Business.Store).Capture(foreign.Owner));Clean(f);
    }

    [Theory]
    [InlineData("Database captured")]
    [InlineData("File copied")]
    public void InterruptedCaptureRemovesItsKnownWorkspaceAndPreservesTheSource(string point)
    {
        using var f=new LedgerFixture();var file=Imported(f);var sources=new ArchiveSources(f.Business.Store){FailurePoint=stage=>{if(stage==point)throw new IOException("Fictional capture interruption");}};
        Assert.Throws<IOException>(()=>sources.Capture(f.Business.Owner));Clean(f);Assert.Equal(file,new ManagedAttachments(f.Business.Store).Get(f.Business.Owner,file.Version.Id));
        using var stream=new ManagedAttachments(f.Business.Store).OpenRead(f.Business.Owner,file.Version.Id);Assert.Equal(file.Version.ByteLength,stream.Length);
    }

    [Fact]
    public void CancellationAfterDatabaseCaptureCannotReturnAnEmptyStoreAsCompleted()
    {
        using var f=new LedgerFixture();using var cancellation=new CancellationTokenSource();
        var sources=new ArchiveSources(f.Business.Store){FailurePoint=point=>{if(point=="Database captured")cancellation.Cancel();}};
        Assert.Throws<OperationCanceledException>(()=>sources.Capture(f.Business.Owner,cancellation.Token));Clean(f);Assert.Empty(f.Book.Posted(f.Business.Owner));
    }
}
