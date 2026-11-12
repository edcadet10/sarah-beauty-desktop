using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableResourceTablesTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];
    [Fact]
    public void SupplierPrivacyAndUnknownVersusZeroLoanAssetFactsRemainDistinctWithoutPosting()
    {
        using var f=new LedgerFixture();const string secret="Fictional-private-resource-sentinel";var book=new BusinessResources(f.Business.Store);var supplier=book.SaveSupplier(f.Business.Owner,new(){Name="Fictional readable supplier",Phone="+1 (555) 010-1234",Email="fictional@example.invalid",Address="Fictional business address",Terms="Fictional supplier terms",PrivateNotes=secret});var loan=book.SaveLoan(f.Business.Owner,new(){Name="Fictional unknown opening",EvidenceId=secret});var zero=book.SaveAsset(f.Business.Owner,new(){Name="Fictional zero registered asset",CostCents=0,TaxBasisCents=0,BusinessUseFraction=0,Funding="Contributed",EvidenceId=secret});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableResourceTables.Build(source,new());Assert.Equal(4,tables.Length);Assert.DoesNotContain(secret,JsonSerializer.Serialize(tables));var suppliers=tables.Single(t=>t.Name=="Suppliers");Assert.Equal(supplier.Email,Cell(suppliers,suppliers.Rows.Single(),"Email"));var loans=tables.Single(t=>t.Name=="Loan register");Assert.Equal("",Cell(loans,loans.Rows.Single(r=>r[0]==loan.Id),"OpeningPrincipalCents"));Assert.Equal("0",Cell(loans,loans.Rows.Single(r=>r[0]==f.Loan.Id),"OpeningPrincipalCents"));var assets=tables.Single(t=>t.Name=="Asset register");Assert.Equal("",Cell(assets,assets.Rows.Single(r=>r[0]==f.Asset.Id),"RegisteredCostCents"));Assert.Equal("0",Cell(assets,assets.Rows.Single(r=>r[0]==zero.Id),"RegisteredCostCents"));Assert.Equal("0",Cell(assets,assets.Rows.Single(r=>r[0]==zero.Id),"BusinessUseFraction"));Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Movements;"));
    }
    [Fact]
    public void ActualRegisteredFactReviewRetainsEarlierUnknownValuesWithoutChangingBookValue()
    {
        using var f=new LedgerFixture();const string secret="Fictional-private-asset-review";var reviewed=new AssetFacts(f.Business.Store).Review(f.Business.Owner,new(f.Asset.Id,0,25000,secret,secret),f.Asset.Revision,Guid.NewGuid().ToString(),BusinessFixture.Phrase);using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableResourceTables.Build(source,new());Assert.DoesNotContain(secret,JsonSerializer.Serialize(tables));var reviews=tables.Single(t=>t.Name=="Asset registered fact reviews");var row=Assert.Single(reviews.Rows);Assert.Equal("",Cell(reviews,row,"PreviousRegisteredCostCents"));Assert.Equal("",Cell(reviews,row,"PreviousTaxBasisCents"));Assert.Equal("0",Cell(reviews,row,"RegisteredCostCents"));Assert.Equal("25000",Cell(reviews,row,"TaxBasisCents"));Assert.Equal(reviewed.FactHistory.Single().RequestId,row[0]);Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM JournalLines;"));
    }
}
