using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class CancellationSourceTests
{
    internal static PaymentApplication Apply(LedgerFixture f,string payment,DocumentVersion invoice,long cents,DateOnly? date=null)
    {
        var book=new PaymentApplications(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,new(){Name="Fictional cancellation source allocation",PaymentId=payment,InvoiceVersionId=invoice.Id,Cents=cents,Date=date??f.Date});
        return book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Single();
    }

    [Fact]
    public void ReviewRetainsTermsAndPaidSourcesWithoutChangingBookingMoneyOrHistory()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000,0.1m);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000,BookingId=invoice.BookingId,EvidenceId="Fictional receipt evidence"});
        Apply(f,receipt.Id,invoice,11000);long before=Convert.ToInt64(TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;"));
        var result=new CancellationSources(f.Business.Store).Load(f.Business.Owner,invoice.BookingId,f.Date);
        Assert.Equal(before,result.Watermark);Assert.Equal("Awaiting deposit",result.BookingStatus);Assert.Equal("TEST",result.Mode);
        Assert.Equal(0,result.Payments.Single().Available.Cents);Assert.Equal(11000,result.Applications.Single().Remaining.Cents);Assert.Equal(0,result.Dues.Sum(d=>d.Cents));
        var terms=result.Documents.Single(d=>d.Id==result.AcceptedQuoteVersionId);Assert.Equal("Accepted",terms.Status);Assert.NotEmpty(terms.AcceptanceEvidence!);
        Assert.Equal(invoice.Hash,result.Documents.Single(d=>d.Id==invoice.Id).Hash);
        Assert.Single(f.Book.Posted(f.Business.Owner));Assert.Equal(11000,f.Balance("1010"));Assert.Equal(-1000,f.Balance("2000"));
        Assert.Equal(before,Convert.ToInt64(TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")));
    }

    [Fact]
    public void SharedReceiptShowsAllocationsToOtherBookingsWithoutAssigningTheirMoneyToThisOne()
    {
        using var f=new LedgerFixture();var first=ApplicationTests.Invoice(f,10000);var second=ApplicationTests.Invoice(f,8000);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,15000));Apply(f,receipt.Id,first,10000);Apply(f,receipt.Id,second,5000);
        var result=new CancellationSources(f.Business.Store).Load(f.Business.Owner,first.BookingId,f.Date);
        Assert.Equal(0,result.Payments.Single().Available.Cents);Assert.Equal(2,result.Applications.Length);
        Assert.Equal(10000,result.Applications.Single(a=>a.InvoiceBookingId==first.BookingId).Remaining.Cents);
        Assert.Equal(5000,result.Applications.Single(a=>a.InvoiceBookingId==second.BookingId).Remaining.Cents);
        Assert.All(result.Documents,d=>Assert.Equal(first.BookingId,d.BookingId));Assert.Single(result.Charges);
    }

    [Fact]
    public void HistoricalReviewExposesFutureReservationsAndChargeReductionsSeparately()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f);var receipt=f.Post(f.Draft(FinancialKind.Receipt,12000));
        var application=Apply(f,receipt.Id,invoice,6000,f.Date.AddDays(2));
        var credits=new CreditBook(f.Business.Store);var creditDraft=credits.SaveDraft(f.Business.Owner,new(){Name="Fictional future credit",PaymentId=receipt.Id,Cents=3000,Date=f.Date.AddDays(3)});
        credits.Approve(f.Business.Owner,creditDraft.Id,creditDraft.Revision,BusinessFixture.Phrase);
        var notes=new CreditNoteBook(f.Business.Store);var noteDraft=notes.SaveDraft(f.Business.Owner,new(){Name="Fictional future reduction",InvoiceVersionId=invoice.Id,IssueDate=f.Date.AddDays(4),Portions=[new(invoice.Snapshot.Lines[0].ChargeId,0.25m)]});
        notes.Issue(f.Business.Owner,noteDraft.Id,noteDraft.Revision,BusinessFixture.Phrase);
        var apps=new PaymentApplications(f.Business.Store);var reversal=apps.SaveDraft(f.Business.Owner,new(){Name="Fictional future unapplication",ReversesId=application.Id,Cents=2000,Date=f.Date.AddDays(5)});
        apps.Approve(f.Business.Owner,reversal.Id,reversal.Revision,BusinessFixture.Phrase);
        var result=new CancellationSources(f.Business.Store).Load(f.Business.Owner,invoice.BookingId,f.Date.AddDays(1));
        Assert.Equal(12000,result.Payments.Single().Available.Cents);Assert.Equal(5000,result.Payments.Single().AvailableAfterAllDatedActions.Cents);
        Assert.Equal(0,result.Applications.Single().Remaining.Cents);Assert.Equal(4000,result.Applications.Single().RemainingAfterAllDatedActions.Cents);
        Assert.Equal(0,result.Credits.Single().Available.Cents);Assert.Equal(3000,result.Credits.Single().AvailableAfterAllDatedActions.Cents);
        Assert.Equal(1m,result.Charges.Single().RemainingQuantity);Assert.Equal(0.75m,result.Charges.Single().RemainingQuantityAfterAllDatedActions);
        Assert.Equal(12000,result.Dues.Sum(d=>d.Cents));
    }

    [Fact]
    public void HeldAndClassifiedValueRetainTheirCommonSourceWithoutInventingAnotherCashReceipt()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f);
        var held=f.Post(f.Draft(FinancialKind.Receipt,12000) with{CounterAccountId=f.Accounts["2200"].Id,BookingId=invoice.BookingId});
        var classified=f.Post(f.Draft(FinancialKind.ReceiptClassification,5000) with{OriginalId=held.Id,CustomerId=f.CustomerId,CounterAccountId=f.Accounts["4000"].Id,EvidenceId="Fictional reviewed classification"});
        var result=new CancellationSources(f.Business.Store).Load(f.Business.Owner,invoice.BookingId,f.Date);
        Assert.Equal(2,result.Payments.Length);Assert.True(result.Payments.Single(p=>p.Id==held.Id).AdditionalCashReceipt);
        var source=result.Payments.Single(p=>p.Id==classified.Id);Assert.False(source.AdditionalCashReceipt);Assert.Equal(held.Id,source.OriginalId);
        Assert.Equal(7000,result.Payments.Single(p=>p.Id==held.Id).Available.Cents);Assert.Equal(5000,source.Available.Cents);Assert.Equal(12000,f.Balance("1010"));
    }

    [Fact]
    public void CancelledPostingAndClosedPeriodRemainExplicitAtTheirActualDates()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f);
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,12000) with{BookingId=invoice.BookingId});
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=receipt.Id,Date=f.Date.AddDays(2)});
        f.Business.Files.Execute($"UPDATE BookSettings SET ClosedThrough='{f.Date:yyyy-MM-dd}';");
        var book=new CancellationSources(f.Business.Store);var historical=book.Load(f.Business.Owner,invoice.BookingId,f.Date);
        Assert.Equal(f.Date,historical.ClosedThrough);Assert.Equal(f.Date.AddDays(2),historical.Payments.Single().CancelledOn);
        Assert.Equal(12000,historical.Payments.Single().Available.Cents);Assert.Equal(0,historical.Payments.Single().AvailableAfterAllDatedActions.Cents);
        Assert.Equal(0,book.Load(f.Business.Owner,invoice.BookingId,f.Date.AddDays(2)).Payments.Single().Available.Cents);
        using var foreign=new BusinessFixture();Assert.Throws<UnauthorizedAccessException>(()=>book.Load(foreign.Owner,invoice.BookingId,f.Date));
        Assert.Throws<ArgumentException>(()=>book.Load(f.Business.Owner,"foreign-booking",f.Date));
    }

    [Fact]
    public void ConcurrentEditsDoNotMixGenerationsAndReviewDoesNotExposePrivateFields()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f);f.Business.Files.Execute("PRAGMA journal_mode=WAL;");
        var bookings=new BookingBook(f.Business.Store);var original=bookings.List(f.Business.Owner).Single();
        var booking=bookings.Save(f.Business.Owner,original with{PrivateNotes="PRIVATE-BOOKING"});
        var customers=new CustomerBook(f.Business.Store);var customer=customers.Search(f.Business.Owner).Single(c=>c.Id==f.CustomerId);
        customer=customers.Save(f.Business.Owner,customer with{PrivateNotes="PRIVATE-CUSTOMER"});
        f.Post(f.Draft(FinancialKind.Receipt,12000) with{BookingId=booking.Id});
        var sources=new CancellationSources(f.Business.Store){AfterBookingRead=()=>customers.Save(f.Business.Owner,customer with{FullName="Fictional later payer"})};
        var old=sources.Load(f.Business.Owner,booking.Id,f.Date);Assert.Equal("Fictional payer",old.Payments.Single().CustomerName);
        Assert.DoesNotContain("PRIVATE-",JsonSerializer.Serialize(old));sources.AfterBookingRead=null;
        var changed=sources.Load(f.Business.Owner,booking.Id,f.Date);Assert.Equal("Fictional later payer",changed.Payments.Single().CustomerName);Assert.True(changed.Watermark>old.Watermark);
    }
}
