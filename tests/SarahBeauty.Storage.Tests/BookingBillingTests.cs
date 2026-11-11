using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class BookingBillingTests
{
    private static PaymentApplication Apply(LedgerFixture f,DocumentVersion invoice,long cents,string payment="",string credit="")
    {
        var book=new PaymentApplications(f.Business.Store);var draft=book.SaveDraft(f.Business.Owner,new PaymentApplicationDraft{
            Name="Fictional booking billing application",InvoiceVersionId=invoice.Id,PaymentId=payment,CreditId=credit,Cents=cents,Date=f.Date});
        return Assert.Single(book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
    }

    private static DocumentVersion Credit(LedgerFixture f,DocumentVersion invoice,decimal quantity,DateOnly date)
    {
        var book=new CreditNoteBook(f.Business.Store);var draft=book.SaveDraft(f.Business.Owner,new CreditNoteDraft{
            Name="Fictional agreed billing reduction",InvoiceVersionId=invoice.Id,IssueDate=date,Portions=[new(invoice.Snapshot.Lines.Single().ChargeId,quantity)]});
        return book.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }

    [Fact]
    public void SharedReceiptsFundedCreditsAndDatedUnapplicationReconcileWithoutNewCash()
    {
        using var f=new LedgerFixture();var first=ApplicationTests.Invoice(f,10000,0.1m);var second=ApplicationTests.Invoice(f,5000,0.1m);
        var shared=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000});
        Apply(f,first,5500,payment:shared.Id);Apply(f,second,5500,payment:shared.Id);
        var other=f.Post(f.Draft(FinancialKind.Receipt,5500) with{TaxCents=500});
        var credits=new CreditBook(f.Business.Store);var draft=credits.SaveDraft(f.Business.Owner,new CreditDraft{Name="Fictional funded booking credit",PaymentId=other.Id,Cents=5500,Date=f.Date});
        var credit=credits.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);var applied=Apply(f,first,5500,credit:credit.Id);
        var apps=new PaymentApplications(f.Business.Store);var reversal=apps.SaveDraft(f.Business.Owner,new PaymentApplicationDraft{
            Name="Fictional partial unapplication",ReversesId=applied.Id,Cents=2750,Date=f.Date.AddDays(1)});
        apps.Approve(f.Business.Owner,reversal.Id,reversal.Revision,BusinessFixture.Phrase);Credit(f,first,0.25m,f.Date.AddDays(2));
        long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        var sources=new BookingBillingSources(f.Business.Store);
        var original=sources.Load(f.Business.Owner,first.BookingId,f.Date);var invoice=Assert.Single(original.Invoices);
        Assert.Equal(11000,invoice.AppliedCents);Assert.Equal(0,invoice.OpenCents);Assert.Empty(original.Credits);
        Assert.Equal(2750,Assert.Single(sources.Load(f.Business.Owner,first.BookingId,f.Date.AddDays(1)).Invoices).OpenCents);
        var reduced=sources.Load(f.Business.Owner,first.BookingId,f.Date.AddDays(2));invoice=Assert.Single(reduced.Invoices);
        Assert.Equal(7500,invoice.EffectiveNetCents);Assert.Equal(750,invoice.EffectiveTaxCents);Assert.Equal(8250,invoice.AppliedCents);Assert.Equal(0,invoice.OpenCents);
        Assert.Equal(first.Id,Assert.Single(reduced.Credits).InvoiceVersionId);
        Assert.Equal(5500,Assert.Single(sources.Load(f.Business.Owner,second.BookingId,f.Date).Invoices).AppliedCents);
        Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);Assert.Equal(-15000,f.Balance("4000"));
        Assert.Equal(watermark,(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!);
    }

    [Fact]
    public void RevisedQuoteAndReissuedInvoiceDoNotSubtractAnOlderCreditTwice()
    {
        using var f=new LedgerFixture();var original=ApplicationTests.Invoice(f,10000,0.1m);var note=Credit(f,original,1,f.Date.AddDays(1));
        var bookings=new BookingBook(f.Business.Store);var booking=bookings.List(f.Business.Owner).Single();
        booking=bookings.Save(f.Business.Owner,booking with{Charges=booking.Charges.Select(c=>c with{UnitCents=12000}).ToArray()});
        var quotes=new QuoteBook(f.Business.Store);var previous=quotes.Versions(f.Business.Owner).Single(v=>v.Snapshot.Kind==DocumentKind.Quote);
        var draft=quotes.SaveDraft(f.Business.Owner,quotes.FromBooking(f.Business.Owner,booking,previous.DocumentId) with{IssueDate=f.Date.AddDays(1)});
        var quote=quotes.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);quotes.Accept(f.Business.Owner,quote.Id,f.Date.AddDays(1),"Fictional amended agreement");
        var invoices=new InvoiceBook(f.Business.Store);var replacement=invoices.SaveDraft(f.Business.Owner,new InvoiceDraft{
            Name="Fictional revised invoice",QuoteVersionId=quote.Id,PayerCustomerId=f.CustomerId,IssueDate=f.Date.AddDays(1),
            Portions=quote.Snapshot.Lines.Select(l=>new InvoicePortion(l.ChargeId,l.Quantity)).ToArray()});
        var issued=invoices.Issue(f.Business.Owner,replacement.Id,replacement.Revision,BusinessFixture.Phrase);
        var sources=new BookingBillingSources(f.Business.Store);var earlier=sources.Load(f.Business.Owner,booking.Id,f.Date);
        Assert.Equal(10000,Assert.Single(earlier.Invoices).EffectiveNetCents);Assert.Empty(earlier.Credits);
        var current=sources.Load(f.Business.Owner,booking.Id,f.Date.AddDays(1));Assert.Equal(2,current.Invoices.Length);
        Assert.Equal(12000,current.Invoices.Sum(i=>i.EffectiveNetCents));Assert.Equal(13200,current.Dues.Sum(d=>d.Cents));
        Assert.Equal(0,current.Invoices.Single(i=>i.VersionId==original.Id).EffectiveNetCents);
        Assert.Equal(quote.Id,current.Invoices.Single(i=>i.VersionId==issued.Id).SourceQuoteVersionId);Assert.Equal(note.Id,Assert.Single(current.Credits).VersionId);
    }

    [Fact]
    public void FutureDocumentsFrozenPayersAndPrivateFieldsStaySeparateFromCurrentBookingFacts()
    {
        using var f=new LedgerFixture();using var foreign=new BusinessFixture();var invoice=ApplicationTests.Invoice(f,10000);
        var bookings=new BookingBook(f.Business.Store);var booking=bookings.List(f.Business.Owner).Single();
        bookings.Save(f.Business.Owner,booking with{Name="Fictional renamed booking",PrivateNotes="PRIVATE-BILLING-SENTINEL"});
        var customers=new CustomerBook(f.Business.Store);var payer=customers.Search(f.Business.Owner).Single();
        customers.Save(f.Business.Owner,payer with{FullName="Fictional current payer name"});
        var sources=new BookingBillingSources(f.Business.Store);var before=sources.Load(f.Business.Owner,booking.Id,f.Date.AddDays(-1));
        Assert.Empty(before.Invoices);Assert.Empty(before.Dues);Assert.Equal("Fictional renamed booking",before.CurrentBookingName);
        var current=sources.Load(f.Business.Owner,booking.Id,f.Date);Assert.Equal("Fictional payer",Assert.Single(current.Invoices).PayerName);
        Assert.All(current.Dues,d=>Assert.Equal("Fictional payer",d.CustomerName));
        Assert.Equal(invoice.Hash,Assert.Single(current.Invoices).SourceHash);Assert.DoesNotContain("PRIVATE-",JsonSerializer.Serialize(current));
        Assert.Throws<ArgumentException>(()=>new BookingBillingSources(foreign.Store).Load(foreign.Owner,booking.Id,f.Date));
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void ConcurrentCreditCannotMixBillingRowsBalancesOrWatermark()
    {
        using var f=new LedgerFixture();f.Business.Files.Execute("PRAGMA journal_mode=WAL;");var invoice=ApplicationTests.Invoice(f,10000);
        long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        var sources=new BookingBillingSources(f.Business.Store){AfterBookingRead=()=>Credit(f,invoice,0.5m,f.Date.AddDays(1))};
        var snapshot=sources.Load(f.Business.Owner,invoice.BookingId,f.Date.AddDays(1));
        Assert.Equal(watermark,snapshot.Watermark);Assert.Empty(snapshot.Credits);Assert.Equal(10000,Assert.Single(snapshot.Invoices).EffectiveNetCents);
        Assert.Equal(10000,snapshot.Dues.Sum(d=>d.Cents));
        var current=new BookingBillingSources(f.Business.Store).Load(f.Business.Owner,invoice.BookingId,f.Date.AddDays(1));
        Assert.Equal(5000,Assert.Single(current.Invoices).EffectiveNetCents);Assert.Single(current.Credits);Assert.True(current.Watermark>snapshot.Watermark);
    }
}
