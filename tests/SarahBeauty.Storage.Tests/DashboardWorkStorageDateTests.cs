using System.Globalization;
using SarahBeauty.Business.Dashboard;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class DashboardWorkStorageDateTests
{
    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void ReceivedValueReviewIncludesItsReservedShareAndExcludesFutureReceipts(string culture)
    {
        using var f=new LedgerFixture();
        var current=f.Post(f.Draft(FinancialKind.UnclassifiedReceipt,10000) with{CustomerId=f.CustomerId});
        var future=f.Post(f.Draft(FinancialKind.UnclassifiedReceipt,20000) with{CustomerId=f.CustomerId,Date=f.Date.AddDays(2)});
        var credits=new CreditBook(f.Business.Store);
        var draft=credits.SaveDraft(f.Business.Owner,new(){Name="Fictional reserved unclassified value",PaymentId=current.Id,Cents=4000,Date=f.Date});
        credits.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        long watermark=Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));
        var previous=CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);
            var result=new DashboardOperationsSources(f.Business.Store).Load(f.Business.Owner,f.Date.AddDays(1));
            var work=Assert.Single(result.Work,w=>w.Kind=="Received-value review");
            Assert.Equal(current.Id,work.RecordId);Assert.Equal(10000,work.RelatedCents);
            Assert.DoesNotContain(result.Work,w=>w.RecordId==future.Id);
        }
        finally{CultureInfo.CurrentCulture=previous;}
        Assert.Equal(watermark,TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));
        Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);Assert.Equal(0,f.Balance("4000"));
    }
}
