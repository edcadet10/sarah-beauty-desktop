using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Recovery;

namespace SarahBeauty.Storage.Tests;

public sealed class ArchiveRestoreTests
{
    private static string Input(LedgerFixture f)=>Path.Combine(f.Business.Files.Folder,"fictional-backup.age");
    private static string ReviewParent(LedgerFixture f)
    {
        string path=Path.Combine(f.Business.Files.Folder,"fictional-recovery-review");Directory.CreateDirectory(path);return path;
    }
    private static void Clean(string parent)=>Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(parent,"RecoveryStaging")));
    private static async Task<(RecoveryMaterial Key,CreatedArchive Archive)> Create(LedgerFixture f)
    {
        f.Post(f.Draft(FinancialKind.Receipt,10000));var invoice=ApplicationTests.Invoice(f,10000);new DocumentArtifacts(f.Business.Store).Render(f.Business.Owner,invoice.Id);
        new ManagedAttachments(f.Business.Store).Import(f.Business.Owner,ManagedAttachmentTests.Pdf(f.Business),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());
        var key=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);return(key,await new FullArchives(f.Business.Store).CreateAsync(f.Business.Owner,Input(f),key.Recipient));
    }
    private static void Write(ZipArchive zip,string name,byte[] bytes)
    {
        zip.GetEntry(name)?.Delete();using var target=zip.CreateEntry(name).Open();target.Write(bytes);
    }
    private static async Task<string> Changed(LedgerFixture f,RecoveryMaterial key,Action<ZipArchive> change)
    {
        string package=Path.Combine(f.Business.Files.Folder,"fictional-edited.zip"),altered=Path.Combine(f.Business.Files.Folder,"fictional-edited.age");var cipher=new RecoveryCipher();
        await cipher.DecryptAsync(Input(f),package,key.ProtectedIdentity,BusinessFixture.Phrase);using(var zip=ZipFile.Open(package,ZipArchiveMode.Update))change(zip);
        await cipher.EncryptAsync(package,altered,key.Recipient);return altered;
    }
    private static JsonObject Manifest(ZipArchive zip)
    {
        using var input=zip.GetEntry("manifest.json")!.Open();return JsonNode.Parse(input)!.AsObject();
    }
    private static void WriteManifest(ZipArchive zip,JsonObject manifest)=>Write(zip,"manifest.json",JsonSerializer.SerializeToUtf8Bytes(manifest));

    [Fact]
    public async Task RecordedArchiveStagesExactReadOnlyFactsWithoutChangingActiveDataAndDisposesPlaintext()
    {
        using var f=new LedgerFixture();var backup=await Create(f);f.Post(f.Draft(FinancialKind.Expense,2500));string parent=ReviewParent(f),database;
        using(var review=await new ArchiveRestores().StageAsync(Input(f),parent,backup.Key.ProtectedIdentity,BusinessFixture.Phrase,backup.Archive))
        {
            Assert.True(review.MatchedRecordedArchive);Assert.Equal(backup.Archive.Sha256,review.ArchiveHash);Assert.Equal(backup.Archive.ManifestHash,review.ManifestHash);Assert.Equal(backup.Archive.Manifest.Watermark,review.Manifest.Watermark);
            database=review.FilePath("sarahbeauty.db");Assert.Equal(1L,TestStore.Scalar(database,"SELECT COUNT(*) FROM Movements;"));Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);Assert.Equal(7500,f.Balance("1010"));
            foreach(var file in review.Manifest.Files)Assert.Equal(file.Sha256,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(review.FilePath(file.RelativePath)))));
            Assert.DoesNotContain(Directory.EnumerateFiles(parent,"*",SearchOption.TopDirectoryOnly),p=>p.EndsWith(".db"));
        }
        Assert.False(File.Exists(database));Clean(parent);Assert.Equal(7500,f.Balance("1010"));
    }

    [Fact]
    public async Task ArchiveWithoutLocalHistoryStillRequiresCompleteValidationAndIsLabeledUnmatched()
    {
        using var f=new LedgerFixture();var backup=await Create(f);string parent=ReviewParent(f);
        using(var review=await new ArchiveRestores().StageAsync(Input(f),parent,backup.Key.ProtectedIdentity,BusinessFixture.Phrase))Assert.False(review.MatchedRecordedArchive);
        Clean(parent);
    }

    [Theory]
    [InlineData("../outside.pdf")]
    [InlineData("C:/outside.pdf")]
    [InlineData("Attachments/../outside.pdf")]
    [InlineData("sarahbeauty.db:private")]
    [InlineData("Attachments\\00000000000000000000000000000001.pdf")]
    public async Task UnsafeOrNoncanonicalZipEntriesNeverEscapeTheReviewWorkspace(string path)
    {
        using var f=new LedgerFixture();var backup=await Create(f);string parent=ReviewParent(f);
        string altered=await Changed(f,backup.Key,zip=>{using var output=zip.CreateEntry(path).Open();output.WriteByte(1);});
        await Assert.ThrowsAsync<InvalidDataException>(()=>new ArchiveRestores().StageAsync(altered,parent,backup.Key.ProtectedIdentity,BusinessFixture.Phrase));Clean(parent);Assert.False(File.Exists(Path.Combine(parent,"outside.pdf")));Assert.Equal(10000,f.Balance("1010"));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("link")]
    [InlineData("missing")]
    [InlineData("hash")]
    [InlineData("version")]
    [InlineData("schema")]
    [InlineData("mode")]
    [InlineData("watermark")]
    [InlineData("facts")]
    [InlineData("null-files")]
    [InlineData("size-limit")]
    [InlineData("unreferenced-file")]
    [InlineData("omitted-reference")]
    [InlineData("database-journal")]
    public async Task AuthenticatedButInvalidPackagesCannotPassRestoreReview(string defect)
    {
        using var f=new LedgerFixture();var backup=await Create(f);string parent=ReviewParent(f);
        string altered=await Changed(f,backup.Key,zip=>
        {
            var manifest=Manifest(zip);
            switch(defect)
            {
                case "duplicate":using(var output=zip.CreateEntry("sarahbeauty.db").Open())output.WriteByte(1);return;
                case "link":zip.GetEntry("sarahbeauty.db")!.ExternalAttributes=unchecked((int)0xa1ff0000);return;
                case "missing":zip.GetEntry(manifest["Files"]![1]!["RelativePath"]!.GetValue<string>())!.Delete();return;
                case "hash":manifest["Files"]![0]!["Sha256"]=new string('0',64);break;
                case "version":manifest["Version"]=2;break;
                case "schema":manifest["SchemaVersion"]=9999;break;
                case "mode":manifest["Mode"]="LIVE";break;
                case "watermark":manifest["Watermark"]=999999;break;
                case "facts":manifest["Facts"]!["TotalDebits"]="0";break;
                case "null-files":manifest["Files"]=null;break;
                case "size-limit":manifest["Files"]![0]!["ByteLength"]=ArchiveManifestChecks.MaximumBytes+1;break;
                case "unreferenced-file":
                    string name="Attachments/00000000000000000000000000000001.pdf";byte[] bytes=[1];Write(zip,name,bytes);
                    manifest["Files"]!.AsArray().Add(JsonSerializer.SerializeToNode(new ArchiveFile(name,"Attachment",1,Convert.ToHexString(SHA256.HashData(bytes)))));break;
                case "omitted-reference":
                    var omitted=manifest["Files"]!.AsArray()[1]!;zip.GetEntry(omitted["RelativePath"]!.GetValue<string>())!.Delete();manifest["Files"]!.AsArray().RemoveAt(1);break;
                case "database-journal":
                    string database=Path.Combine(f.Business.Files.Folder,"fictional-invalid-journal.db");
                    using(var input=zip.GetEntry("sarahbeauty.db")!.Open())using(var output=File.Create(database))input.CopyTo(output);
                    using(var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=database,Pooling=false}.ToString()))
                    {
                        connection.Open();using var command=connection.CreateCommand();command.CommandText="UPDATE JournalLines SET DebitCents=DebitCents+1 WHERE DebitCents>0;";command.ExecuteNonQuery();
                    }
                    byte[] changed=File.ReadAllBytes(database);Write(zip,"sarahbeauty.db",changed);manifest["Files"]![0]!["ByteLength"]=changed.LongLength;manifest["Files"]![0]!["Sha256"]=Convert.ToHexString(SHA256.HashData(changed));break;
            }
            WriteManifest(zip,manifest);
        });
        await Assert.ThrowsAsync<InvalidDataException>(()=>new ArchiveRestores().StageAsync(altered,parent,backup.Key.ProtectedIdentity,BusinessFixture.Phrase));Clean(parent);Assert.Equal(10000,f.Balance("1010"));
    }

    [Theory]
    [InlineData("Archive decrypted",false)]
    [InlineData("File extracted",false)]
    [InlineData("Database verified",false)]
    [InlineData("Archive decrypted",true)]
    [InlineData("File extracted",true)]
    [InlineData("Database verified",true)]
    public async Task InterruptedOrCancelledReviewPreservesTheActiveStoreAndRemovesKnownPlaintext(string stage,bool cancel)
    {
        using var f=new LedgerFixture();var backup=await Create(f);string parent=ReviewParent(f);using var cancellation=new CancellationTokenSource();
        var restores=new ArchiveRestores{FailurePoint=point=>{if(point!=stage)return;if(cancel)cancellation.Cancel();else throw new IOException("Fictional interrupted recovery review");}};
        if(cancel)await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>restores.StageAsync(Input(f),parent,backup.Key.ProtectedIdentity,BusinessFixture.Phrase,cancellation:cancellation.Token));
        else await Assert.ThrowsAsync<IOException>(()=>restores.StageAsync(Input(f),parent,backup.Key.ProtectedIdentity,BusinessFixture.Phrase));
        Clean(parent);Assert.Equal(10000,f.Balance("1010"));Assert.Single(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public async Task WrongRecoveryCorruptionAndWrongRecordedFactsCannotProduceAReview()
    {
        using var f=new LedgerFixture();var backup=await Create(f);string parent=ReviewParent(f);var restores=new ArchiveRestores();
        await Assert.ThrowsAsync<RecoveryFailure>(()=>restores.StageAsync(Input(f),parent,backup.Key.ProtectedIdentity,"fictional wrong recovery phrase"));Clean(parent);
        await Assert.ThrowsAsync<InvalidDataException>(()=>restores.StageAsync(Input(f),parent,backup.Key.ProtectedIdentity,BusinessFixture.Phrase,backup.Archive with{Sha256=new string('0',64)}));Clean(parent);
        await Assert.ThrowsAsync<InvalidDataException>(()=>restores.StageAsync(Input(f),parent,backup.Key.ProtectedIdentity,BusinessFixture.Phrase,backup.Archive with{ManifestHash=new string('0',64)}));Clean(parent);
        byte[] changed=await File.ReadAllBytesAsync(Input(f));changed[^1]^=1;await File.WriteAllBytesAsync(Input(f),changed);
        await Assert.ThrowsAsync<RecoveryFailure>(()=>restores.StageAsync(Input(f),parent,backup.Key.ProtectedIdentity,BusinessFixture.Phrase));Clean(parent);Assert.Equal(10000,f.Balance("1010"));
    }
}
