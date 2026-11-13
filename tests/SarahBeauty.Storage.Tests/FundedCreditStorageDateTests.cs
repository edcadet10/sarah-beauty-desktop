using System.Globalization;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class FundedCreditStorageDateTests
{
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void CreditIssueRetryApplicationAndUnusedReturnKeepTheOriginalIssueDate(string culture)
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,5000);var payment=f.Post(f.Draft(FinancialKind.Receipt,10000));var book=new CreditBook(f.Business.Store);var draft=book.SaveDraft(f.Business.Owner,new(){Name="Fictional funded credit decision",PaymentId=payment.Id,Cents=6000,Date=f.Date});var previous=CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);var credit=book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);Assert.Equal(f.Date,credit.IssueDate);long watermark=Watermark(f);Assert.Equal(credit,book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));Assert.Equal(watermark,Watermark(f));
            var apps=new PaymentApplications(f.Business.Store);var apply=apps.SaveDraft(f.Business.Owner,new(){Name="Fictional funded credit use",CreditId=credit.Id,InvoiceVersionId=invoice.Id,Cents=2500,Date=f.Date});apps.Approve(f.Business.Owner,apply.Id,apply.Revision,BusinessFixture.Phrase);
            var release=book.SaveDraft(f.Business.Owner,new(){Action=CreditAction.ReturnToReceipt,Name="Fictional unused credit release",CreditId=credit.Id,Cents=2000,Date=f.Date.AddDays(1)});book.Approve(f.Business.Owner,release.Id,release.Revision,BusinessFixture.Phrase);
            Assert.Equal(3500,book.Available(f.Business.Owner,credit.Id,f.Date).Cents);Assert.Equal(1500,book.Available(f.Business.Owner,credit.Id,f.Date.AddDays(1)).Cents);Assert.Equal(4000,apps.Available(f.Business.Owner,payment.Id,f.Date).Cents);Assert.Equal(6000,apps.Available(f.Business.Owner,payment.Id,f.Date.AddDays(1)).Cents);
        }
        finally{CultureInfo.CurrentCulture=previous;}
        Assert.Equal("2027-01-10",TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT IssueDate FROM CustomerCredits;"));Assert.Equal(f.Date,Assert.Single(book.Credits(f.Business.Owner)).IssueDate);Assert.Equal(2L,TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT count(*) FROM CreditFunding;"));Assert.Single(f.Book.Posted(f.Business.Owner));Assert.Equal(10000,f.Balance("1010"));Assert.Equal(-10000,f.Balance("4000"));
    }
    private static long Watermark(LedgerFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));
}
