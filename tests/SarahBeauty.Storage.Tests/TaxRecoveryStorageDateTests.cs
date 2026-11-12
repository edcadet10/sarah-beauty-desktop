using System.Globalization;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class TaxRecoveryStorageDateTests
{
    [Theory]
    [InlineData("ar-SA",TaxRecoveryDestination.Cash)][InlineData("th-TH",TaxRecoveryDestination.Cash)]
    [InlineData("ar-SA",TaxRecoveryDestination.Offset)][InlineData("th-TH",TaxRecoveryDestination.Offset)]
    [InlineData("ar-SA",TaxRecoveryDestination.WriteOff)][InlineData("th-TH",TaxRecoveryDestination.WriteOff)]
    public void TaxRecoveryAndReversalKeepTheSameDatedBalanceAndRejectExcess(string culture,string destination)
    {
        using var f=new LedgerFixture();var payment=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000});f.Post(f.Draft(FinancialKind.TaxRemittance,1000));var refund=f.Post(f.Draft(FinancialKind.Refund,11000) with{OriginalId=payment.Id,TaxCents=1000,TaxRefundAllocation=new(0,1000,0),EvidenceId="Fictional recoverable refund assessment"});if(destination==TaxRecoveryDestination.Offset)f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000});long cash=f.Balance("1010"),income=f.Balance("4000"),tax=f.Balance("2000");int movements=f.Book.Posted(f.Business.Owner).Count;var previous=CultureInfo.CurrentCulture;
        FinancialDraft Recovery(long amount)=>f.Draft(FinancialKind.TaxRecovery,amount) with{OriginalId=refund.Id,TaxRecoveryDestination=destination,Date=f.Date.AddDays(1),EvidenceId="Fictional actual recovery evidence"};
        try
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);var recovered=f.Post(Recovery(400));f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=recovered.Id,Date=f.Date.AddDays(2)});var excess=f.Book.SaveDraft(f.Business.Owner,Recovery(1001) with{Date=f.Date.AddDays(3)});long watermark=Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Contains("remaining recoverable tax",Assert.Throws<ArgumentException>(()=>f.Book.Approve(f.Business.Owner,excess.Id,excess.Revision,BusinessFixture.Phrase)).Message,StringComparison.Ordinal);Assert.Equal(watermark,TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));
        }
        finally{CultureInfo.CurrentCulture=previous;}
        Assert.Equal(1000,f.Balance("1250"));Assert.Equal(cash,f.Balance("1010"));Assert.Equal(income,f.Balance("4000"));Assert.Equal(tax,f.Balance("2000"));Assert.Equal(movements+2,f.Book.Posted(f.Business.Owner).Count);
    }
}
