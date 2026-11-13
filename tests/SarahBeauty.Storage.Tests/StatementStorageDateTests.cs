using System.Globalization;
using SarahBeauty.Business.Banking;

namespace SarahBeauty.Storage.Tests;

public sealed class StatementStorageDateTests
{
    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void ImportStoresGregorianPeriodAndBankLineDates(string culture)
    {
        using var f=new LedgerFixture();
        var book=new StatementBook(f.Business.Store);
        var draft=Draft(f);
        var previous=CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);
            Import(f,book,draft,Guid.NewGuid().ToString());
        }
        finally{CultureInfo.CurrentCulture=previous;}
        Assert.Equal("2027-01-01",TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT PeriodStart FROM StatementImports;"));
        Assert.Equal("2027-01-31",TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT PeriodEnd FROM StatementImports;"));
        Assert.Equal("2027-01-10",TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Date FROM BankLines;"));
        AssertDates(f,book,draft);
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void ExistingImportReadsAndExactRetryKeepTheOriginalDates(string culture)
    {
        using var f=new LedgerFixture();
        var book=new StatementBook(f.Business.Store);
        var draft=Draft(f);
        string request=Guid.NewGuid().ToString();
        var imported=Import(f,book,draft,request);
        long watermark=Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));
        var previous=CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);
            AssertDates(f,book,draft);
            Assert.Equal(imported.Id,Import(f,book,draft,request).Id);
            Assert.Equal(imported.Id,book.Preview(f.Business.Owner,draft).ExistingImportId);
        }
        finally{CultureInfo.CurrentCulture=previous;}
        Assert.Equal(watermark,TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));
        Assert.Equal(1L,TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT COUNT(*) FROM Audit WHERE Action='Import reviewed statement';"));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    private static StatementDraft Draft(LedgerFixture f)=>new(f.Accounts["1010"].Id,new(2027,1,1),new(2027,1,31),0,2500,
        StatementCsv.Parse("Date,Description,Amount\n2027-01-10,Fictional date boundary deposit,25.00",new()));

    private static StatementImport Import(LedgerFixture f,StatementBook book,StatementDraft draft,string request)=>
        book.Import(f.Business.Owner,draft,book.Preview(f.Business.Owner,draft).Generation,[],request,BusinessFixture.Phrase);

    private static void AssertDates(LedgerFixture f,StatementBook book,StatementDraft draft)
    {
        var imported=Assert.Single(book.List(f.Business.Owner));
        Assert.Equal(draft.From,imported.From);
        Assert.Equal(draft.Through,imported.Through);
        Assert.Equal(f.Date,Assert.Single(book.BankLines(f.Business.Owner,draft.AccountId)).Date);
        var row=Assert.Single(book.SourceRows(f.Business.Owner,imported.Id));
        Assert.Equal(f.Date,row.Line.Date);
        Assert.Equal(f.Date,row.Source.Date);
    }
}
