using System.Security.Cryptography;
using System.Text;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class DocumentIntegrityTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ChangedSnapshotOrHashIsRejectedByReportsAndOtherSourceReaders(bool changeJson)
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000);
        f.Business.Files.Execute("DROP TRIGGER DocumentVersions_Immutable;");
        f.Business.Store.Transaction(db=>
        {
            if(changeJson)db.Execute("UPDATE DocumentVersions SET DataJson=replace(DataJson,'Fictional service','Altered fictional service') WHERE Id=$id;",("$id",invoice.Id));
            else db.Execute("UPDATE DocumentVersions SET SnapshotHash=$hash WHERE Id=$id;",("$hash",new string('0',64)),("$id",invoice.Id));
            return true;
        });
        Assert.Throws<InvalidOperationException>(()=>new FinancialReports(f.Business.Store).Load(f.Business.Owner,f.Date,f.Date));
        Assert.Throws<InvalidOperationException>(()=>new QuoteBook(f.Business.Store).Versions(f.Business.Owner));
        Assert.Throws<InvalidOperationException>(()=>new DocumentArtifacts(f.Business.Store).Get(f.Business.Owner,invoice.Id));
        Assert.Throws<InvalidOperationException>(()=>new CancellationSources(f.Business.Store).Load(f.Business.Owner,invoice.BookingId,f.Date));
    }

    [Fact]
    public void ValidOriginalJsonIsVerifiedBeforeDeserializationWithoutReserializingOlderShapes()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000);
        f.Business.Files.Execute("DROP TRIGGER DocumentVersions_Immutable;");
        string hash=f.Business.Store.Transaction(db=>
        {
            string json="  "+(string)db.Scalar("SELECT DataJson FROM DocumentVersions WHERE Id=$id;",("$id",invoice.Id))!+"\n";
            string expected=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
            db.Execute("UPDATE DocumentVersions SET DataJson=$json,SnapshotHash=$hash WHERE Id=$id;",("$json",json),("$hash",expected),("$id",invoice.Id));return expected;
        });
        var loaded=new QuoteBook(f.Business.Store).Versions(f.Business.Owner).Single(v=>v.Id==invoice.Id);
        Assert.Equal(hash,loaded.Hash);Assert.Equal(10000,loaded.Snapshot.NetCents);
        Assert.Equal(10000,Assert.Single(new FinancialReports(f.Business.Store).Load(f.Business.Owner,f.Date,f.Date).Customers).OpenCents);
    }
}
