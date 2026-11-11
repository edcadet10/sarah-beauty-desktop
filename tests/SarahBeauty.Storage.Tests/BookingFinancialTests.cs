using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class BookingFinancialTests
{
    private static BookingFinancialSnapshot Read(LedgerFixture f,string booking,int month)=>new BookingFinancialSources(f.Business.Store)
        .Load(f.Business.Owner,booking,new(2027,month,1),new(2027,month,DateTime.DaysInMonth(2027,month)));

    [Fact]
    public void DirectCostAndServiceIncomeExcludeFundingDepreciationAndDisposalResults()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000);
        PostedMovement Post(FinancialDraft draft)=>f.Post(draft with{BookingId=invoice.BookingId});
        Post(f.Draft(FinancialKind.Receipt,10000) with{TaxCents=1000,FeeCents=300});
        Post(f.Draft(FinancialKind.Receipt,5000) with{CounterAccountId=f.Accounts["2200"].Id});
        Post(f.Draft(FinancialKind.Expense,2000));Post(f.Draft(FinancialKind.OwnerPaidExpense,1000));
        Post(f.Draft(FinancialKind.AssetPurchase,3000));Post(f.Draft(FinancialKind.LoanProceeds,1000));
        Post(f.Draft(FinancialKind.LoanPayment,600) with{PrincipalCents=500,InterestCents=80,FeeCents=20});
        Post(f.Draft(FinancialKind.Depreciation,500));Post(f.Draft(FinancialKind.Contribution,900));
        Post(f.Draft(FinancialKind.AssetDisposal,1000) with{AssetId=f.Asset.Id,EvidenceId="Fictional disposal evidence"});
        var snapshot=Read(f,invoice.BookingId,1);
        Assert.Equal(3300,snapshot.RecordedDirectCostCents);Assert.Equal(3300,snapshot.PeriodRecordedDirectCostCents);
        Assert.Equal(9000,snapshot.RecognizedServiceIncomeCents);Assert.Equal(15000,snapshot.Cash.Sum(c=>c.SignedGrossCents));
        Assert.Equal(1000,snapshot.Cash.Sum(c=>c.SignedTaxCents));Assert.Equal(300,snapshot.Cash.Sum(c=>c.SignedFeeCents));
        Assert.Equal(14700,snapshot.Cash.Sum(c=>c.SignedNetCashCents));Assert.Equal(2,snapshot.Cash.Length);
        Assert.DoesNotContain(snapshot.Entries,e=>e.IsDirectCost && e.AccountCode is "5030" or "5035" or "5040" or "5060");
        Assert.Contains(snapshot.Entries,e=>e.AccountCode=="5060" && !e.IsDirectCost);
    }

    [Fact]
    public void RefundAndPostingCancellationKeepOriginalTaxFeesAndActualDates()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000);
        var received=f.Post(f.Draft(FinancialKind.Receipt,11000) with{BookingId=invoice.BookingId,TaxCents=1000,FeeCents=300});
        var refunded=f.Post(f.Draft(FinancialKind.Refund,3300) with{OriginalId=received.Id,Date=new(2027,2,10),TaxCents=300});
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=refunded.Id,Date=new(2027,3,10)});
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=received.Id,Date=new(2027,4,10)});
        var january=Read(f,invoice.BookingId,1);Assert.Equal(10700,january.Cash.Sum(c=>c.SignedNetCashCents));
        var february=Read(f,invoice.BookingId,2);Assert.Equal(7700,february.Cash.Sum(c=>c.SignedGrossCents));Assert.Equal(700,february.Cash.Sum(c=>c.SignedTaxCents));
        var march=Read(f,invoice.BookingId,3);var restored=Assert.Single(march.Cash,c=>c.Date.Month==3);
        Assert.Equal(3300,restored.SignedGrossCents);Assert.Equal(300,restored.SignedTaxCents);Assert.Equal(10700,march.Cash.Sum(c=>c.SignedNetCashCents));
        var april=Read(f,invoice.BookingId,4);var cancelled=Assert.Single(april.Cash,c=>c.Date.Month==4);
        Assert.Equal(-11000,cancelled.SignedGrossCents);Assert.Equal(-1000,cancelled.SignedTaxCents);Assert.Equal(-300,cancelled.SignedFeeCents);
        Assert.Equal(0,april.Cash.Sum(c=>c.SignedNetCashCents));Assert.Equal(0,april.RecognizedServiceIncomeCents);Assert.Equal(0,april.RecordedDirectCostCents);
        Assert.Equal(-10000,april.PeriodRecognizedServiceIncomeCents);Assert.Equal(-300,april.PeriodRecordedDirectCostCents);
    }

    [Fact]
    public void SharedPaymentApplicationDoesNotInventACashReceiptOrBookingTag()
    {
        using var f=new LedgerFixture();using var foreign=new BusinessFixture();var invoice=ApplicationTests.Invoice(f,10000);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,10000));
        var apps=new PaymentApplications(f.Business.Store);var draft=apps.SaveDraft(f.Business.Owner,new PaymentApplicationDraft{
            Name="Fictional shared payment allocation",PaymentId=receipt.Id,InvoiceVersionId=invoice.Id,Cents=10000,Date=f.Date});
        apps.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        var sources=new BookingFinancialSources(f.Business.Store);var financial=sources.Load(f.Business.Owner,invoice.BookingId,f.Date,f.Date);
        Assert.Empty(financial.Cash);Assert.Empty(financial.Entries);Assert.Equal(0,financial.RecognizedServiceIncomeCents);
        Assert.Equal(10000,Assert.Single(new BookingBillingSources(f.Business.Store).Load(f.Business.Owner,invoice.BookingId,f.Date).Invoices).AppliedCents);
        Assert.Throws<ArgumentException>(()=>sources.Load(f.Business.Owner,invoice.BookingId,f.Date.AddDays(1),f.Date));
        Assert.Throws<ArgumentException>(()=>new BookingFinancialSources(foreign.Store).Load(foreign.Owner,invoice.BookingId,f.Date,f.Date));
    }

    [Fact]
    public void SupplierReturnKeepsOriginalBookingAndDatedCostReduction()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000);var other=ApplicationTests.Invoice(f,10000);
        var spent=f.Post(f.Draft(FinancialKind.Expense,2000) with{BookingId=invoice.BookingId});
        var returned=f.Post(f.Draft(FinancialKind.SupplierRefund,1000) with{OriginalId=spent.Id,BookingId=other.BookingId,Date=new(2027,2,10)});
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=returned.Id,Date=new(2027,3,10)});
        Assert.Equal(2000,Read(f,invoice.BookingId,1).RecordedDirectCostCents);
        var february=Read(f,invoice.BookingId,2);Assert.Equal(1000,february.RecordedDirectCostCents);Assert.Equal(-1000,february.PeriodRecordedDirectCostCents);
        Assert.Equal(2000,Read(f,invoice.BookingId,3).RecordedDirectCostCents);Assert.Empty(Read(f,other.BookingId,3).Entries);
        Assert.Empty(february.Cash);
    }

    [Fact]
    public void ConcurrentExpenseCannotMixSourcesTotalsPrivateFieldsOrWatermark()
    {
        using var f=new LedgerFixture();f.Business.Files.Execute("PRAGMA journal_mode=WAL;");var invoice=ApplicationTests.Invoice(f,10000);
        var bookings=new BookingBook(f.Business.Store);var booking=bookings.List(f.Business.Owner).Single();
        bookings.Save(f.Business.Owner,booking with{PrivateNotes="PRIVATE-FINANCIAL-SENTINEL"});
        f.Post(f.Draft(FinancialKind.Expense,2000) with{BookingId=booking.Id});
        long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        var sources=new BookingFinancialSources(f.Business.Store){AfterBookingRead=()=>f.Post(f.Draft(FinancialKind.Expense,1000) with{BookingId=booking.Id})};
        var snapshot=sources.Load(f.Business.Owner,booking.Id,f.Date,f.Date);
        Assert.Equal(watermark,snapshot.Watermark);Assert.Equal(2000,snapshot.RecordedDirectCostCents);Assert.DoesNotContain("PRIVATE-",JsonSerializer.Serialize(snapshot));
        Assert.Equal(3000,Read(f,booking.Id,1).RecordedDirectCostCents);
    }
}
