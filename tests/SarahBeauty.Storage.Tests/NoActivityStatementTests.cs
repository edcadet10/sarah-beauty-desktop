using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class NoActivityStatementTests
{
    private static StatementDraft Draft(LedgerFixture f)=>new(f.Accounts["1010"].Id,new(2027,1,1),new(2027,1,31),12345,12345,
        new([],"","{\"Kind\":\"Confirmed no-activity statement\"}"),"Fictional no-activity statement evidence");

    [Fact]
    public void ConfirmedNoActivityRetainsEvidenceWithoutInventingBankOrLedgerRows()
    {
        using var f=new LedgerFixture();
        var book=new StatementBook(f.Business.Store);
        var draft=Draft(f);
        var preview=book.Preview(f.Business.Owner,draft);
        Assert.Empty(preview.Rows);
        string request=Guid.NewGuid().ToString();
        var imported=book.Import(f.Business.Owner,draft,preview.Generation,[],request,BusinessFixture.Phrase);
        Assert.Equal(imported.Id,book.Import(f.Business.Owner,draft,preview.Generation,[],request,BusinessFixture.Phrase).Id);
        Assert.Equal(0,imported.RowCount);
        Assert.Equal(draft.EvidenceId,book.List(f.Business.Owner).Single().EvidenceId);
        Assert.Empty(book.SourceRows(f.Business.Owner,imported.Id));
        Assert.Empty(book.BankLines(f.Business.Owner,draft.AccountId));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
        Assert.Equal(0,f.Balance("1010"));
    }

    [Fact]
    public void NoActivityRequiresEvidenceAndEqualExplicitBalances()
    {
        using var f=new LedgerFixture();
        var book=new StatementBook(f.Business.Store);
        var draft=Draft(f);
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{EvidenceId=""}));
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{OpeningCents=null}));
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{ClosingCents=12346}));
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{Source=draft.Source with{ContentHash=new string('A',64)}}));
        var zero=draft with{OpeningCents=0,ClosingCents=0};
        var preview=book.Preview(f.Business.Owner,zero);
        var saved=book.Import(f.Business.Owner,zero,preview.Generation,[],Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        Assert.Equal(0,saved.ClosingCents);
    }

    [Fact]
    public void VersionTwentyOneUpgradePreservesLegacyRequestHashesAndExactRetries()
    {
        using var f=new LedgerFixture();
        var book=new StatementBook(f.Business.Store);
        var source=StatementCsv.Parse("Date,Description,Amount\n2027-01-10,Fictional deposit,25",new());
        var draft=new StatementDraft(f.Accounts["1010"].Id,new(2027,1,1),new(2027,1,31),0,2500,source);
        var preview=book.Preview(f.Business.Owner,draft);
        string request=Guid.NewGuid().ToString();
        var imported=book.Import(f.Business.Owner,draft,preview.Generation,[],request,BusinessFixture.Phrase);
        var legacy=new{draft.AccountId,draft.From,draft.Through,draft.OpeningCents,draft.ClosingCents,draft.Source};
        string expected=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{Draft=legacy,Choices=Array.Empty<StatementRowChoice>()}))));
        Assert.Equal(expected,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT PayloadHash FROM StatementImports;"));
        f.Business.Files.Execute("ALTER TABLE StatementImports DROP COLUMN EvidenceId; PRAGMA user_version=21;");
        Assert.Equal(BusinessSchema.Version,LocalDatabase.InitializeTestDatabaseAt(f.Business.Files.DatabasePath).SchemaVersion);
        Assert.Single(f.Business.Files.Snapshots());
        var retry=book.Import(f.Business.Owner,draft,preview.Generation,[],request,BusinessFixture.Phrase);
        Assert.Equal(imported.Id,retry.Id);
        Assert.Equal("",retry.EvidenceId);
        Assert.Single(book.BankLines(f.Business.Owner,draft.AccountId));
    }
}
