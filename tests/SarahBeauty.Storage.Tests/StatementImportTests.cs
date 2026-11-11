using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class StatementImportTests
{
    private static StatementDraft Draft(LedgerFixture f,string csv,long opening=0,string? account=null,StatementCsvMapping? mapping=null)
    {
        var source=StatementCsv.Parse(csv,mapping??new());
        return new(account??f.Accounts["1010"].Id,new(2027,1,1),new(2027,1,31),opening,checked(opening+source.NetCents),source);
    }

    private static StatementImport Import(LedgerFixture f,StatementBook book,StatementDraft draft,params StatementRowChoice[] choices)=>
        book.Import(f.Business.Owner,draft,book.Preview(f.Business.Owner,draft).Generation,choices,Guid.NewGuid().ToString(),BusinessFixture.Phrase);

    [Fact]
    public void ImportPersistsReviewedSourcesOnceWithoutCreatingMoney()
    {
        using var f=new LedgerFixture();
        var book=new StatementBook(f.Business.Store);
        var draft=Draft(f,"Date,Description,Amount\n2027-01-10,Fictional deposit,100.00\n2027-01-11,Fictional fee,-25.00");
        var preview=book.Preview(f.Business.Owner,draft);
        string request=Guid.NewGuid().ToString();
        var imported=book.Import(f.Business.Owner,draft,preview.Generation,[],request,BusinessFixture.Phrase);
        Assert.Equal(imported.Id,book.Import(f.Business.Owner,draft,preview.Generation,[],request,BusinessFixture.Phrase).Id);
        Assert.Equal(2,book.BankLines(f.Business.Owner,draft.AccountId).Count);
        Assert.Equal(2,book.SourceRows(f.Business.Owner,imported.Id).Count);
        Assert.Equal(f.Business.Owner.OwnerId,imported.ApprovedBy);
        Assert.Equal(7500,imported.ClosingCents);
        Assert.Equal(imported.Id,book.Preview(f.Business.Owner,draft).ExistingImportId);
        Assert.Throws<ArgumentException>(()=>Import(f,book,draft));
        Assert.Throws<ArgumentException>(()=>book.Import(f.Business.Owner,draft with{OpeningCents=1,ClosingCents=7501},preview.Generation,[],request,BusinessFixture.Phrase));
        Assert.Single(new StatementBook(new BusinessStore(f.Business.Files.DatabasePath)).List(f.Business.Owner));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
        Assert.Equal(0,f.Balance("1010"));
        Assert.Equal(1L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Audit WHERE Action='Import reviewed statement';"));
    }

    [Fact]
    public void OverlappingStatementsReuseReviewedBankIdentityAndKeepBothSourceRows()
    {
        using var f=new LedgerFixture();
        var book=new StatementBook(f.Business.Store);
        var first=Import(f,book,Draft(f,"Date,Description,Amount\n2027-01-10,Fictional deposit,100\n2027-01-11,Fictional fee,-25"));
        string fee=book.SourceRows(f.Business.Owner,first.Id).Single(r=>r.Source.SignedCents<0).Line.Id;
        var overlap=Draft(f,"Date,Description,Amount\n2027-01-11,Fictional fee updated description,-25\n2027-01-12,Fictional extra deposit,25",10000);
        Assert.Single(book.Preview(f.Business.Owner,overlap).Rows[0].Candidates);
        Assert.Throws<ArgumentException>(()=>Import(f,book,overlap));
        var second=Import(f,book,overlap,new StatementRowChoice(2,fee,"Same real bank fee in an overlapping export"));
        Assert.Equal(3,book.BankLines(f.Business.Owner,overlap.AccountId).Count);
        var retained=book.SourceRows(f.Business.Owner,second.Id).First();
        Assert.Equal(fee,retained.Line.Id);
        Assert.Contains("updated description",retained.Source.Description);
        Assert.Equal("Fictional fee",retained.Line.Description);
        Assert.Equal(2,book.SourceRows(f.Business.Owner,first.Id).Count);
    }

    [Fact]
    public void IdenticalLegitimateRowsRemainDistinctAfterExplicitReview()
    {
        using var f=new LedgerFixture();
        var book=new StatementBook(f.Business.Store);
        var same=Draft(f,"Date,Description,Amount\n2027-01-10,Fictional cash deposit,25\n2027-01-10,Fictional cash deposit,25");
        Assert.All(book.Preview(f.Business.Owner,same).Rows,r=>Assert.True(r.SimilarInSource));
        Assert.Throws<ArgumentException>(()=>Import(f,book,same));
        var statement=Import(f,book,same,new(2,Reason:"First distinct real cash deposit"),new(3,Reason:"Second distinct real cash deposit"));
        Assert.Equal(2,book.SourceRows(f.Business.Owner,statement.Id).Select(r=>r.Line.Id).Distinct().Count());
        var extra=Draft(f,"Date,Description,Amount\n2027-01-10,Fictional third real deposit,25");
        Assert.Equal(2,book.Preview(f.Business.Owner,extra).Rows[0].Candidates.Length);
        Import(f,book,extra,new StatementRowChoice(2,Reason:"Third distinct real cash deposit supported by separate evidence"));
        Assert.Equal(3,book.BankLines(f.Business.Owner,same.AccountId).Count);
    }

    [Fact]
    public void ASourceCannotUseTheSameExistingLineTwiceOrLinkAnotherAccountsLine()
    {
        using var f=new LedgerFixture();
        var book=new StatementBook(f.Business.Store);
        var first=Import(f,book,Draft(f,"Date,Description,Amount\n2027-01-10,Fictional deposit,25"));
        string id=book.SourceRows(f.Business.Owner,first.Id).Single().Line.Id;
        var repeated=Draft(f,"Date,Description,Amount\n2027-01-10,Fictional first,25\n2027-01-10,Fictional second,25");
        Assert.Throws<ArgumentException>(()=>Import(f,book,repeated,new(2,id,"Review first"),new(3,id,"Review second")));
        Assert.Throws<ArgumentException>(()=>Import(f,book,repeated with{AccountId=f.Accounts["1020"].Id},new(2,id,"Foreign account"),new(3,Reason:"Different transaction")));
        Assert.Single(book.List(f.Business.Owner));
    }

    [Fact]
    public void UniqueReferencesCannotBeReimportedAsNewOrSilentlyChangeDateOrAmount()
    {
        using var f=new LedgerFixture();
        var book=new StatementBook(f.Business.Store);
        var mapping=new StatementCsvMapping(ReferenceColumn:"Ref",ReliableReferences:true);
        var first=Import(f,book,Draft(f,"Date,Description,Amount,Ref\n2027-01-10,Fictional original,25,bank-1",mapping:mapping));
        string id=book.SourceRows(f.Business.Owner,first.Id).Single().Line.Id;
        var overlap=Draft(f,"Date,Description,Amount,Ref\n2027-01-10,Fictional updated,25,bank-1",mapping:mapping);
        Assert.Throws<ArgumentException>(()=>Import(f,book,overlap,new StatementRowChoice(2,Reason:"Claim as new")));
        Import(f,book,overlap,new StatementRowChoice(2,id,"Same unique bank reference"));
        var changed=Draft(f,"Date,Description,Amount,Ref\n2027-01-11,Fictional changed date,25,bank-1",mapping:mapping);
        Assert.Throws<ArgumentException>(()=>Import(f,book,changed,new StatementRowChoice(2,id,"Incorrect date match")));
        Assert.Single(book.BankLines(f.Business.Owner,overlap.AccountId));
    }

    [Theory]
    [InlineData("statement-header")]
    [InlineData("bank-line")]
    [InlineData("statement-row")]
    [InlineData("audit")]
    public void InterruptedImportRollsBackEveryStageAndRetryCreatesOneStatement(string stage)
    {
        using var f=new LedgerFixture();
        var book=new StatementBook(f.Business.Store);
        var draft=Draft(f,"Date,Description,Amount\n2027-01-10,Fictional deposit,25");
        var preview=book.Preview(f.Business.Owner,draft);
        string request=Guid.NewGuid().ToString();
        book.FailurePoint=p=>{if(p==stage)throw new IOException("Injected statement import failure");};
        Assert.Throws<IOException>(()=>book.Import(f.Business.Owner,draft,preview.Generation,[],request,BusinessFixture.Phrase));
        Assert.Empty(book.List(f.Business.Owner));
        Assert.Empty(book.BankLines(f.Business.Owner,draft.AccountId));
        book.FailurePoint=null;
        book.Import(f.Business.Owner,draft,preview.Generation,[],request,BusinessFixture.Phrase);
        Assert.Single(book.List(f.Business.Owner));
        Assert.Single(book.BankLines(f.Business.Owner,draft.AccountId));
    }

    [Fact]
    public void BalanceDateAccountAndStaleReviewErrorsFailBeforeImport()
    {
        using var f=new LedgerFixture();
        var book=new StatementBook(f.Business.Store);
        var draft=Draft(f,"Date,Description,Amount\n2027-01-10,Fictional deposit,25");
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{ClosingCents=2501}));
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{OpeningCents=null}));
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{Through=f.Date.AddDays(-1)}));
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{AccountId=f.Accounts["4000"].Id}));
        var preview=book.Preview(f.Business.Owner,draft);
        Import(f,book,Draft(f,"Date,Description,Amount\n2027-01-12,Fictional other deposit,50"));
        Assert.Throws<ArgumentException>(()=>book.Import(f.Business.Owner,draft,preview.Generation,[],Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        Assert.Single(book.List(f.Business.Owner));
    }

    [Fact]
    public void ImportedSourcesAreImmutableAndVersionNineteenUpgradeKeepsItsSnapshot()
    {
        using var f=new LedgerFixture();
        f.Business.Files.Execute("DROP TABLE BankMatches; DROP TABLE BankMatchGroups; DROP TABLE StatementRows; DROP TABLE BankLines; DROP TABLE StatementImports; PRAGMA user_version=19;");
        Assert.Equal(BusinessSchema.Version,LocalDatabase.InitializeTestDatabaseAt(f.Business.Files.DatabasePath).SchemaVersion);
        Assert.Single(f.Business.Files.Snapshots());
        var book=new StatementBook(f.Business.Store);
        var statement=Import(f,book,Draft(f,"Date,Description,Amount\n2027-01-10,Fictional deposit,25"));
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute("UPDATE BankLines SET SignedCents=1;"));
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute("DELETE FROM StatementRows;"));
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute("UPDATE StatementImports SET ClosingCents=0;"));
        Assert.Throws<ArgumentException>(()=>new ChartOfAccounts(f.Business.Store).Save(f.Business.Owner,f.Accounts["1010"] with{Type="Expense",CashKind="None"}));
        Assert.Single(book.SourceRows(f.Business.Owner,statement.Id));
    }
}
