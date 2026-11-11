using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class CancellationPreviewTests
{
    internal static CancellationDraft Draft(LedgerFixture f,DocumentVersion invoice)=>new()
    {Name="Fictional agreed cancellation",BookingId=invoice.BookingId,Date=f.Date.AddDays(1),RequestedDate=f.Date,
        AgreementReview="Fictional owner reviewed the issued terms and this separate agreement",EvidenceId="Fictional signed agreement",
        TermsVersionId=invoice.Id,ClientReason="Fictional client-facing charge reduction"};
    internal static CancellationValueChoice Value(LedgerFixture f,string payment,long retain=0,long refund=0,long tax=0,long transfer=0,string credit="")=>
        new(payment,credit,retain,refund,tax,transfer,RefundCashAccountId:f.Accounts["1010"].Id,RefundMethod:"Fictional bank refund",RefundEvidenceId:"Fictional actual refund evidence",RefundTaxResolved:true);

    [Fact]
    public void MixedPreviewUsesTheRealCombinedRulesAndRollsBackEveryEffect()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000,0.1m);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000});var application=CancellationSourceTests.Apply(f,receipt.Id,invoice,11000);
        var draft=Draft(f,invoice) with{Unapplications=[new(application.Id,11000)],Reductions=[new(invoice.Id,[new(invoice.Snapshot.Lines[0].ChargeId,1)])],
            Values=[Value(f,receipt.Id,retain:2200,refund:3300,tax:300,transfer:5500)]};
        var before=new CancellationSources(f.Business.Store).Load(f.Business.Owner,invoice.BookingId,f.Date.AddDays(1));
        var result=new CancellationCases(f.Business.Store).Preview(f.Business.Owner,draft);
        Assert.Equal("Cancelled",result.After.BookingStatus);Assert.Equal(0,result.After.Dues.Sum(d=>d.Cents));
        Assert.Equal(new ValueBalance(2200,200),result.After.Payments.Single().Available);Assert.Equal(new ValueBalance(5500,500),result.After.Credits.Single().Available);
        Assert.Equal(5,result.Actions.Length);Assert.Equal(3300,result.Journal.Where(j=>j.AccountId==f.Accounts["1010"].Id).Sum(j=>j.CreditCents-j.DebitCents));
        Assert.Equal(300,result.Journal.Where(j=>j.AccountId==f.Accounts["2000"].Id).Sum(j=>j.DebitCents-j.CreditCents));
        var after=new CancellationSources(f.Business.Store).Load(f.Business.Owner,invoice.BookingId,f.Date.AddDays(1));
        Assert.Equal(before.Watermark,after.Watermark);Assert.Equal(before.BookingRevision,after.BookingRevision);Assert.Equal("Awaiting deposit",after.BookingStatus);
        Assert.Empty(after.Credits);Assert.Equal(11000,after.Applications.Single().Remaining.Cents);Assert.Equal(11000,f.Balance("1010"));Assert.Single(f.Book.Posted(f.Business.Owner));
        Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM CreditNoteHeaders;"));
        Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Records WHERE Kind IN('CreditDraft','CreditNoteDraft');"));
        Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM DocumentCounters WHERE Type='CreditNote';"));
    }

    [Fact]
    public void CombinedExcessAndMissingUnapplicationFailWithoutLeavingPartialChanges()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f);var receipt=f.Post(f.Draft(FinancialKind.Receipt,12000));
        var application=CancellationSourceTests.Apply(f,receipt.Id,invoice,12000);var book=new CancellationCases(f.Business.Store);
        var draft=Draft(f,invoice) with{Unapplications=[new(application.Id,12000)],Reductions=[new(invoice.Id,[new(invoice.Snapshot.Lines[0].ChargeId,1)])],
            Values=[Value(f,receipt.Id,refund:6000,transfer:6001)]};
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft));
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{Unapplications=[]}));
        Assert.Equal(12000,new PaymentApplications(f.Business.Store).History(f.Business.Owner).Single().Cents);
        Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM CreditNoteHeaders;"));
        Assert.Empty(new CreditBook(f.Business.Store).Credits(f.Business.Owner));Assert.Single(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void SharedReceiptCannotSpendTheOtherBookingsApplicationOrSourceTwice()
    {
        using var f=new LedgerFixture();var first=ApplicationTests.Invoice(f,10000);var second=ApplicationTests.Invoice(f,5000);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,15000));var own=CancellationSourceTests.Apply(f,receipt.Id,first,10000);var other=CancellationSourceTests.Apply(f,receipt.Id,second,5000);
        var book=new CancellationCases(f.Business.Store);var draft=Draft(f,first) with{Unapplications=[new(own.Id,10000)],Values=[Value(f,receipt.Id,refund:10000)]};
        var preview=book.Preview(f.Business.Owner,draft);Assert.Equal(5000,preview.After.Applications.Single(a=>a.Id==other.Id).Remaining.Cents);
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{Unapplications=[new(other.Id,5000)]}));
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{Values=[Value(f,receipt.Id,refund:10001)]}));
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{Values=[Value(f,receipt.Id,refund:5000),Value(f,receipt.Id,transfer:5000)]}));
    }

    [Fact]
    public void ABackdatedDecisionCannotConsumeValueReservedForALaterInvoice()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f);var receipt=f.Post(f.Draft(FinancialKind.Receipt,12000) with{BookingId=invoice.BookingId});
        CancellationSourceTests.Apply(f,receipt.Id,invoice,12000,f.Date.AddDays(2));
        var draft=Draft(f,invoice) with{Values=[Value(f,receipt.Id,refund:100)]};
        Assert.Throws<ArgumentException>(()=>new CancellationCases(f.Business.Store).Preview(f.Business.Owner,draft));
        Assert.Single(f.Book.Posted(f.Business.Owner));Assert.Equal("Awaiting deposit",new BookingBook(f.Business.Store).List(f.Business.Owner).Single().Status);
    }

    [Fact]
    public void HeldTransfersAndRefundsPreserveClassificationAndExistingCreditFunding()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,12000) with{CounterAccountId=f.Accounts["2200"].Id,BookingId=invoice.BookingId});
        var credits=new CreditBook(f.Business.Store);var creditDraft=credits.SaveDraft(f.Business.Owner,new(){Name="Fictional prior held credit",PaymentId=receipt.Id,Cents=6000,Date=f.Date});
        var credit=credits.Approve(f.Business.Owner,creditDraft.Id,creditDraft.Revision,BusinessFixture.Phrase);
        var draft=Draft(f,invoice) with{Values=[Value(f,receipt.Id,retain:1000,refund:2000,transfer:3000,credit:credit.Id)]};
        var result=new CancellationCases(f.Business.Store).Preview(f.Business.Owner,draft);
        Assert.Equal("Held customer deposit",result.After.Credits.Single().Source.SourceTreatment);Assert.Equal(4000,result.After.Credits.Single().Available.Cents);
        Assert.Equal(credit.Id,result.Actions.Single(a=>a.Kind=="Transfer credit").ResultId);Assert.Equal(6000,result.After.Payments.Single().Available.Cents);
        Assert.DoesNotContain(result.Journal,j=>j.AccountId==f.Accounts["4000"].Id);Assert.Equal(6000,credits.Available(f.Business.Owner,credit.Id,f.Date).Cents);
    }

    [Fact]
    public void ReviewRequiresAnActualAgreementTaxDecisionAndOwnedTermsAndDestination()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,12000) with{BookingId=invoice.BookingId});var book=new CancellationCases(f.Business.Store);
        var draft=Draft(f,invoice);var refund=Value(f,receipt.Id,refund:1000);
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{AgreementReview=""}));
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{RequestedDate=f.Date.AddDays(3)}));
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{TermsVersionId="foreign-terms"}));
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{Values=[refund with{RefundTaxResolved=false}]}));
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{Values=[Value(f,receipt.Id,transfer:1000) with{DestinationBookingId=invoice.BookingId}]}));
        using var foreign=new BusinessFixture();Assert.Throws<UnauthorizedAccessException>(()=>book.Preview(foreign.Owner,draft));
        f.Business.Files.Execute($"UPDATE BookSettings SET ClosedThrough='{draft.Date:yyyy-MM-dd}';");
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{Values=[refund]}));
    }
}
