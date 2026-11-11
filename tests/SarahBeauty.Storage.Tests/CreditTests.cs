using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class CreditTests
{
    private static CustomerCredit Credit(LedgerFixture f,string payment,long cents)
    {
        var book=new CreditBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,new CreditDraft{Name="Fictional funded credit decision",PaymentId=payment,Cents=cents,Date=f.Date});
        return book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }

    private static List<PaymentApplication> Apply(LedgerFixture f,CustomerCredit credit,DocumentVersion invoice,long cents)
    {
        var book=new PaymentApplications(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,new PaymentApplicationDraft{Name="Fictional credit allocation",CreditId=credit.Id,
            InvoiceVersionId=invoice.Id,Cents=cents,Date=f.Date});
        return book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }

    [Fact]
    public void IssuingCreditReservesReceiptValueWithoutCreatingAnotherReceiptOrIncome()
    {
        using var f=new LedgerFixture();
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,32665));
        var book=new CreditBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,new CreditDraft{Name="Fictional transfer decision",PaymentId=receipt.Id,Cents=32665,Date=f.Date});
        var credit=book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(credit.Id,book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Id);
        Assert.Equal(32665,book.Available(f.Business.Owner,credit.Id,f.Date).Cents);
        Assert.Equal(0,new PaymentApplications(f.Business.Store).Available(f.Business.Owner,receipt.Id,f.Date).Cents);
        Assert.Equal(1L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM CreditFunding;"));
        Assert.Single(f.Book.Posted(f.Business.Owner));
        Assert.Equal(-32665,f.Balance("4000"));
        Assert.Equal(32665,f.Balance("1010"));
    }

    [Fact]
    public async Task TwoCreditUsesCannotSpendMoreThanTheFundedValue()
    {
        using var f=new LedgerFixture();
        var first=ApplicationTests.Invoice(f,20000);
        var second=ApplicationTests.Invoice(f,20000);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,32665));
        var credit=Credit(f,receipt.Id,32665);
        var apps=new PaymentApplications(f.Business.Store);
        var drafts=new[]{first,second}.Select(invoice=>apps.SaveDraft(f.Business.Owner,new PaymentApplicationDraft{
            Name="Fictional competing credit use",CreditId=credit.Id,InvoiceVersionId=invoice.Id,Cents=20000,Date=f.Date})).ToArray();
        var results=await Task.WhenAll(drafts.Select(d=>Task.Run(()=>
        {
            try{apps.Approve(f.Business.Owner,d.Id,d.Revision,BusinessFixture.Phrase);return true;}
            catch(ArgumentException){return false;}
        })));
        Assert.Single(results,r=>r);
        Assert.Equal(12665,new CreditBook(f.Business.Store).Available(f.Business.Owner,credit.Id,f.Date).Cents);
        Assert.Equal(0,apps.Available(f.Business.Owner,receipt.Id,f.Date).Cents);
        Assert.Equal(-32665,f.Balance("4000"));
    }

    [Fact]
    public void ReturningUnusedCreditReleasesItsReservationOnce()
    {
        using var f=new LedgerFixture();
        var invoice=ApplicationTests.Invoice(f,20000);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,32665));
        var credit=Credit(f,receipt.Id,32665);
        Apply(f,credit,invoice,20000);
        var book=new CreditBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,new CreditDraft{Name="Fictional return of unused credit",Action=CreditAction.ReturnToReceipt,
            CreditId=credit.Id,Cents=5000,Date=f.Date.AddDays(1)});
        book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(7665,book.Available(f.Business.Owner,credit.Id,f.Date.AddDays(1)).Cents);
        Assert.Equal(5000,new PaymentApplications(f.Business.Store).Available(f.Business.Owner,receipt.Id,f.Date.AddDays(1)).Cents);
        Assert.Equal(2L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM CreditFunding;"));
        Assert.Equal(-32665,f.Balance("4000"));
    }

    [Fact]
    public void UnapplyingCreditRestoresTheCreditButKeepsTheOriginalReceiptReserved()
    {
        using var f=new LedgerFixture();
        var invoice=ApplicationTests.Invoice(f);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,12000));
        var credit=Credit(f,receipt.Id,12000);
        var application=Assert.Single(Apply(f,credit,invoice,12000));
        var apps=new PaymentApplications(f.Business.Store);
        var draft=apps.SaveDraft(f.Business.Owner,new PaymentApplicationDraft{Name="Fictional credit unapplication",ReversesId=application.Id,Cents=12000,Date=f.Date.AddDays(1)});
        apps.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(12000,new CreditBook(f.Business.Store).Available(f.Business.Owner,credit.Id,f.Date.AddDays(1)).Cents);
        Assert.Equal(0,apps.Available(f.Business.Owner,receipt.Id,f.Date.AddDays(1)).Cents);
        Assert.Equal(12000,apps.Outstanding(f.Business.Owner,f.Date.AddDays(1)).Sum(d=>d.Cents));
    }

    [Theory]
    [InlineData("credit-record")]
    [InlineData("credit-funding")]
    public void FailedCreditIssueRollsBackItsValueAndCanRetry(string stage)
    {
        using var f=new LedgerFixture();
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,12000));
        var book=new CreditBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,new CreditDraft{Name="Fictional credit failure fixture",PaymentId=receipt.Id,Cents=12000,Date=f.Date});
        book.FailurePoint=p=>{if(p==stage)throw new IOException("Injected credit failure");};
        Assert.Throws<IOException>(()=>book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Empty(book.Credits(f.Business.Owner));
        Assert.Equal(12000,new PaymentApplications(f.Business.Store).Available(f.Business.Owner,receipt.Id,f.Date).Cents);
        book.FailurePoint=null;
        book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Single(book.Credits(f.Business.Owner));
    }

    [Fact]
    public void CreditTaxComponentsFollowTheirFundingAndDoNotPostTaxAgain()
    {
        using var f=new LedgerFixture();
        var invoice=ApplicationTests.Invoice(f,10000,0.1m);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000});
        var credit=Credit(f,receipt.Id,11000);
        Apply(f,credit,invoice,5500);
        Assert.Equal(new ValueBalance(5500,500),new CreditBook(f.Business.Store).Available(f.Business.Owner,credit.Id,f.Date));
        Assert.Equal(-1000,f.Balance("2000"));
        Assert.Equal(-10000,f.Balance("4000"));
    }
}
