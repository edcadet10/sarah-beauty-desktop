using System.Text.Json;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableBankTablesTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];
    private const string Private="Fictional-private-bank-review-evidence";
    private static StatementImport Import(LedgerFixture f,StatementDraft draft)
    {
        var book=new StatementBook(f.Business.Store);return book.Import(f.Business.Owner,draft,book.Preview(f.Business.Owner,draft).Generation,[],Guid.NewGuid().ToString(),BusinessFixture.Phrase);
    }
    [Fact]
    public void WithdrawnAndCorrectedImportsKeepSeparateSignedRowsAndSourceMappingsWithoutPrivateReviews()
    {
        using var f=new LedgerFixture();const string csv="Date,Description,Amount,Reference\n2027-01-10,=Fictional literal bank name,100.00,fictional-readable-bank-001";
        StatementDraft Draft(bool negate)
        {
            var source=StatementCsv.Parse(csv,new(){NegateAmounts=negate,ReferenceColumn="Reference",ReliableReferences=true});return new(f.Accounts["1010"].Id,new(2027,1,1),new(2027,1,31),0,source.NetCents,source,Private);
        }
        var old=Import(f,Draft(true));var withdrawals=new StatementWithdrawals(f.Business.Store);withdrawals.Approve(f.Business.Owner,old.Id,withdrawals.Review(f.Business.Owner,old.Id).Generation,Private,Private,Guid.NewGuid().ToString(),BusinessFixture.Phrase);var current=Import(f,Draft(false));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableBankTables.Build(source,new());Assert.Equal(7,tables.Length);Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));var statements=tables.Single(t=>t.Name=="Bank statements");Assert.Equal("true",Cell(statements,statements.Rows.Single(r=>r[0]==old.Id),"Withdrawn"));Assert.Equal("false",Cell(statements,statements.Rows.Single(r=>r[0]==current.Id),"Withdrawn"));var rows=tables.Single(t=>t.Name=="Bank imported source rows");Assert.Equal("-10000",Cell(rows,rows.Rows.Single(r=>r[0]==old.Id),"OriginalSignedCents"));Assert.Equal("10000",Cell(rows,rows.Rows.Single(r=>r[0]==current.Id),"OriginalSignedCents"));Assert.All(rows.Rows,r=>Assert.Equal("=Fictional literal bank name",Cell(rows,r,"OriginalDescription")));var mapping=tables.Single(t=>t.Name=="Bank import mappings");Assert.Equal("true",Cell(mapping,mapping.Rows.Single(r=>r[0]==old.Id),"NegateAmounts"));var lines=tables.Single(t=>t.Name=="Bank transactions");Assert.Equal(2,lines.Rows.Count);Assert.Single(lines.Rows,r=>Cell(lines,r,"HasActiveStatement")=="true");Assert.Single(tables.Single(t=>t.Name=="Bank statement withdrawals").Rows);Assert.Empty(f.Book.Posted(f.Business.Owner));
    }
    [Fact]
    public void ActualPartialMatchesAndDatedReleasesRetainBothIdentitiesWithoutAnotherCashPosting()
    {
        using var f=new LedgerFixture();var bank=BankMatchingTests.Statement(f,10000).Single();var cash=BankMatchingTests.Cash(f,f.Post(f.Draft(FinancialKind.Receipt,10000)));var book=new BankMatching(f.Business.Store);BankMatchingTests.Match(f,book,f.Date,new MatchPortion(bank.Id,cash.Id,6000));var match=Assert.Single(book.History(f.Business.Owner));BankMatchingTests.Match(f,book,f.Date.AddDays(1),new MatchPortion(bank.Id,cash.Id,2000,match.Id));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableBankTables.Build(source,new());var portions=tables.Single(t=>t.Name=="Bank match portions");Assert.Equal(2,portions.Rows.Count);var release=portions.Rows.Single(r=>Cell(portions,r,"ReversesMatchId")==match.Id);Assert.Equal("2000",Cell(portions,release,"Cents"));Assert.Equal("2027-01-11",Cell(portions,release,"Date"));Assert.All(portions.Rows,r=>Assert.Equal(cash.Id,Cell(portions,r,"JournalLineId")));Assert.Equal(2,tables.Single(t=>t.Name=="Bank match approvals").Rows.Count);Assert.Single(f.Book.Posted(f.Business.Owner));Assert.Equal(10000,f.Balance("1010"));
    }
    [Fact]
    public void ConfirmedNoActivityStatementHasBalancesAndUnknownMappingWithoutInventedTransactions()
    {
        using var f=new LedgerFixture();var statement=Import(f,new(f.Accounts["1010"].Id,new(2027,1,1),new(2027,1,31),0,0,new([],"","{}"),Private));using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableBankTables.Build(source,new());var header=tables.Single(t=>t.Name=="Bank statements");Assert.Equal("0",Cell(header,Assert.Single(header.Rows),"ClosingCents"));var mapping=tables.Single(t=>t.Name=="Bank import mappings");Assert.Equal(statement.Id,Assert.Single(mapping.Rows)[0]);Assert.Equal("",Cell(mapping,mapping.Rows[0],"NegateAmounts"));Assert.Empty(tables.Single(t=>t.Name=="Bank imported source rows").Rows);Assert.Empty(tables.Single(t=>t.Name=="Bank transactions").Rows);Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
    }
}
