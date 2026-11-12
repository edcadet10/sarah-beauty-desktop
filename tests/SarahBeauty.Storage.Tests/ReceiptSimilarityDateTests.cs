using System.Globalization;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReceiptSimilarityDateTests
{
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void AChangeOfCalendarKeepsTheSamePossibleDuplicateAndItsReversalDate(string culture)
    {
        using var f=new LedgerFixture();var source=f.Post(f.Draft(FinancialKind.Receipt,5000));f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=source.Id,Date=f.Date.AddDays(1)});var book=new ReceiptSimilarities(f.Business.Store);var draft=f.Draft(FinancialKind.Receipt,5000);var expected=book.Find(f.Business.Owner,draft);Assert.Equal(1,expected.Total);Assert.Equal(f.Date.AddDays(1),Assert.Single(expected.Receipts).CancelledOn);long watermark=Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));var previous=CultureInfo.CurrentCulture;
        try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);var actual=book.Find(f.Business.Owner,draft);Assert.Equal(expected.Total,actual.Total);Assert.Equal(expected.Receipts,actual.Receipts);}
        finally{CultureInfo.CurrentCulture=previous;}
        Assert.Equal(watermark,TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
    }
}
