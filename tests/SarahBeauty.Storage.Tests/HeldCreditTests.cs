using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class HeldCreditTests
{
    private static CustomerCredit Issue(LedgerFixture f,string source,long cents,DateOnly? date=null)
    {
        var book=new CreditBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,new(){Name="Fictional held-value transfer agreement",PaymentId=source,Cents=cents,Date=date??f.Date});
        return book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }

    [Fact]
    public void HeldDepositCreditKeepsItsLiabilityAndTaxWhileRefundingFundingOnlyOnce()
    {
        using var f=new LedgerFixture();
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000,CounterAccountId=f.Accounts["2200"].Id});
        var credit=Issue(f,receipt.Id,5500);var book=new CreditBook(f.Business.Store);
        Assert.Equal("Held customer deposit",credit.SourceTreatment);
        Assert.Equal(new ValueBalance(5500,500),book.Available(f.Business.Owner,credit.Id,f.Date));
        Assert.Equal(0,f.Balance("4000"));Assert.Equal(-10000,f.Balance("2200"));Assert.Equal(-1000,f.Balance("2000"));
        var invoice=ApplicationTests.Invoice(f,10000,0.1m);var apps=new PaymentApplications(f.Business.Store);
        var allocation=apps.SaveDraft(f.Business.Owner,new(){Name="Fictional held value needs classification",CreditId=credit.Id,InvoiceVersionId=invoice.Id,Cents=1100,Date=f.Date});
        Assert.Throws<ArgumentException>(()=>apps.Approve(f.Business.Owner,allocation.Id,allocation.Revision,BusinessFixture.Phrase));
        f.Post(f.Draft(FinancialKind.Refund,2200) with{OriginalId=receipt.Id,CreditId=credit.Id,CounterAccountId=f.Accounts["2200"].Id,TaxCents=200});
        Assert.Equal(new ValueBalance(3300,300),book.Available(f.Business.Owner,credit.Id,f.Date));
        Assert.Equal(new ValueBalance(5500,500),apps.Available(f.Business.Owner,receipt.Id,f.Date));
        Assert.Equal(8800,f.Balance("1010"));Assert.Equal(-8000,f.Balance("2200"));Assert.Equal(-800,f.Balance("2000"));Assert.Equal(0,f.Balance("4000"));
    }

    [Fact]
    public void SuspenseCreditNeedsExplicitReleaseAndClassificationBeforeItPaysAnInvoice()
    {
        using var f=new LedgerFixture();var receipt=f.Post(f.Draft(FinancialKind.UnclassifiedReceipt,12000) with{CustomerId=f.CustomerId});
        var held=Issue(f,receipt.Id,12000);Assert.Equal("Unclassified suspense",held.SourceTreatment);
        var book=new CreditBook(f.Business.Store);
        var release=book.SaveDraft(f.Business.Owner,new(){Name="Fictional reviewed release before recognition",Action=CreditAction.ReturnToReceipt,CreditId=held.Id,Cents=6000,Date=f.Date.AddDays(1)});
        book.Approve(f.Business.Owner,release.Id,release.Revision,BusinessFixture.Phrase);
        var classified=f.Post(f.Draft(FinancialKind.ReceiptClassification,6000) with{OriginalId=receipt.Id,CustomerId=f.CustomerId,CounterAccountId=f.Accounts["4000"].Id,Date=f.Date.AddDays(1),EvidenceId="Fictional tax and recognition review"});
        var income=Issue(f,classified.Id,6000,f.Date.AddDays(1));Assert.Equal("Recognized income",income.SourceTreatment);
        var invoice=ApplicationTests.Invoice(f,6000);var apps=new PaymentApplications(f.Business.Store);
        var allocation=apps.SaveDraft(f.Business.Owner,new(){Name="Fictional reviewed credit use",CreditId=income.Id,InvoiceVersionId=invoice.Id,Cents=6000,Date=f.Date.AddDays(1)});
        apps.Approve(f.Business.Owner,allocation.Id,allocation.Revision,BusinessFixture.Phrase);
        Assert.Equal(0,apps.Outstanding(f.Business.Owner,f.Date.AddDays(1)).Sum(d=>d.Cents));
        Assert.Equal(6000,book.Available(f.Business.Owner,held.Id,f.Date.AddDays(1)).Cents);Assert.Equal(0,book.Available(f.Business.Owner,income.Id,f.Date.AddDays(1)).Cents);
        Assert.Equal(12000,f.Balance("1010"));Assert.Equal(-6000,f.Balance("2400"));Assert.Equal(-6000,f.Balance("4000"));
    }

    [Fact]
    public void UnidentifiedSuspenseCannotCreateCreditForAnInventedCustomer()
    {
        using var f=new LedgerFixture();var receipt=f.Post(f.Draft(FinancialKind.UnclassifiedReceipt,12000));
        Assert.Throws<ArgumentException>(()=>Issue(f,receipt.Id,12000));Assert.Empty(new CreditBook(f.Business.Store).Credits(f.Business.Owner));
        Assert.Equal(12000,new PaymentApplications(f.Business.Store).Available(f.Business.Owner,receipt.Id,f.Date).Cents);
    }
}
