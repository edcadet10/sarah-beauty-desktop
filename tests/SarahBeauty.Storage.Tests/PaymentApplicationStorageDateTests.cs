using System.Globalization;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class PaymentApplicationStorageDateTests
{
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void NewApplicationsKeepTheirDateAndCurrentAvailableValue(string culture)
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000);var payment=f.Post(f.Draft(FinancialKind.Receipt,10000));var book=new PaymentApplications(f.Business.Store);var draft=book.SaveDraft(f.Business.Owner,new(){Name="Fictional dated application",PaymentId=payment.Id,InvoiceVersionId=invoice.Id,Cents=6000,Date=f.Date});long watermark=Watermark(f);WithCulture(culture,()=>{Assert.Equal(f.Date,Assert.Single(book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase)).Date);Assert.Equal(4000,book.Available(f.Business.Owner,payment.Id,f.Date).Cents);Assert.Equal(f.Date,Assert.Single(book.History(f.Business.Owner)).Date);});Assert.Equal("2027-01-10",TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Date FROM PaymentApplications;"));Assert.Equal(watermark+1,Watermark(f));Assert.Single(f.Book.Posted(f.Business.Owner));
    }
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void ADatedPartialReversalRestoresOnlyItsValueAndKeepsFinancialFacts(string culture)
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000);var payment=f.Post(f.Draft(FinancialKind.Receipt,10000));var book=new PaymentApplications(f.Business.Store);var draft=book.SaveDraft(f.Business.Owner,new(){Name="Fictional original application",PaymentId=payment.Id,InvoiceVersionId=invoice.Id,Cents=6000,Date=f.Date});var original=Assert.Single(book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));WithCulture(culture,()=>{var undo=book.SaveDraft(f.Business.Owner,new(){Name="Fictional partial unapplication",ReversesId=original.Id,Cents=2500,Date=f.Date.AddDays(1)});Assert.Equal(f.Date.AddDays(1),Assert.Single(book.Approve(f.Business.Owner,undo.Id,undo.Revision,BusinessFixture.Phrase)).Date);Assert.Equal(4000,book.Available(f.Business.Owner,payment.Id,f.Date).Cents);Assert.Equal(6500,book.Available(f.Business.Owner,payment.Id,f.Date.AddDays(1)).Cents);});Assert.Equal(2,book.History(f.Business.Owner).Count);Assert.Single(f.Book.Posted(f.Business.Owner));
    }
    [Theory]
    [InlineData("ar-SA")][InlineData("th-TH")]
    public void ExactApprovedRetryKeepsItsStoredDateAndDoesNotWrite(string culture)
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000);var payment=f.Post(f.Draft(FinancialKind.Receipt,10000));var book=new PaymentApplications(f.Business.Store);var draft=book.SaveDraft(f.Business.Owner,new(){Name="Fictional retained allocation",PaymentId=payment.Id,InvoiceVersionId=invoice.Id,Cents=6000,Date=f.Date});var original=Assert.Single(book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));long watermark=Watermark(f);WithCulture(culture,()=>Assert.Equal(original,Assert.Single(book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase))));Assert.Equal(watermark,Watermark(f));Assert.Single(book.History(f.Business.Owner));
    }
    private static long Watermark(LedgerFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));
    private static void WithCulture(string name,Action action)
    {
        var previous=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(name);action();}finally{CultureInfo.CurrentCulture=previous;}
    }
}
