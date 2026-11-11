using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;

namespace SarahBeauty.Storage.Tests;

public sealed class InvoiceTests
{
    private static DocumentVersion Quote(BusinessFixture f,Booking? custom=null)
    {
        var book=new BookingBook(f.Store);
        var booking=custom??QuoteTests.Booking(f);
        booking=book.Save(f.Owner,booking with{PayerMode="Single",SinglePayerCustomerId=booking.PrimaryCustomerId,
            UnpaidResponsibility="Fictional single payer covers all charges"});
        var quotes=new QuoteBook(f.Store);
        var draft=quotes.SaveDraft(f.Owner,quotes.FromBooking(f.Owner,booking));
        var issued=quotes.Issue(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        return quotes.Accept(f.Owner,issued.Id,issued.Snapshot.IssueDate,"Fictional acceptance");
    }

    private static InvoiceDraft Draft(BusinessFixture f,DocumentVersion quote,InvoicePortion[]? portions=null)=>
        new(){Name="Fictional invoice",QuoteVersionId=quote.Id,IssueDate=quote.Snapshot.IssueDate,
            PayerCustomerId=new BookingBook(f.Store).List(f.Owner).Single(b=>b.Id==quote.BookingId).PrimaryCustomerId,
            Terms=quote.Snapshot.Terms,Portions=portions??quote.Snapshot.Lines.Select(l=>new InvoicePortion(l.ChargeId,l.Quantity)).ToArray()};

    private static DocumentVersion Issue(BusinessFixture f,InvoiceDraft value)
    {
        var book=new InvoiceBook(f.Store);
        var draft=book.SaveDraft(f.Owner,value);
        return book.Issue(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }

    [Fact]
    public void FullConversionAndRetryCoverEveryChargeAndMilestoneOnce()
    {
        using var f=new BusinessFixture();
        var quote=Quote(f);
        var invoices=new InvoiceBook(f.Store);
        var draft=invoices.SaveDraft(f.Owner,Draft(f,quote));
        var invoice=invoices.Issue(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(145660,invoice.Snapshot.TotalCents);
        Assert.Equal(invoice.Snapshot.TotalCents,invoice.Snapshot.Milestones.Sum(m=>m.Cents));
        Assert.Equal(invoice.Id,invoices.Issue(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Id);
        Assert.All(invoices.Remaining(f.Owner,quote.Id),r=>{Assert.Equal(0,r.Quantity);Assert.Equal(0,r.NetCents);Assert.Equal(0,r.TaxCents);});
        Assert.Throws<ArgumentException>(()=>Issue(f,Draft(f,quote)));
    }

    [Fact]
    public void SplitPayersPreserveTheBookingTotalAndDoNotRepeatTheWholeTrial()
    {
        using var f=new BusinessFixture();
        var book=new BookingBook(f.Store);
        var booking=QuoteTests.Booking(f);
        var other=new CustomerBook(f.Store).Save(f.Owner,new Customer{FullName="Fictional second payer"});
        booking=book.Save(f.Owner,booking with{PayerMode="Split",UnpaidResponsibility="Fictional each payer owes their allocated share",
            Charges=booking.Charges.Select(c=>c.Unit=="Mile"?c with{PayerCustomerId=booking.PrimaryCustomerId}:c).ToArray()});
        var quotes=new QuoteBook(f.Store);
        var qdraft=quotes.SaveDraft(f.Owner,quotes.FromBooking(f.Owner,booking));
        var quote=quotes.Issue(f.Owner,qdraft.Id,qdraft.Revision,BusinessFixture.Phrase);
        quote=quotes.Accept(f.Owner,quote.Id,quote.Snapshot.IssueDate,"Fictional split acceptance");
        var guests=quote.Snapshot.Lines.Single(l=>l.Unit=="Person");
        var first=Issue(f,Draft(f,quote,quote.Snapshot.Lines.Select(l=>new InvoicePortion(l.ChargeId,l==guests?4:l.Quantity)).ToArray()));
        var second=Issue(f,Draft(f,quote,[new(guests.ChargeId,5)]) with{PayerCustomerId=other.Id});
        Assert.Equal(quote.Snapshot.TotalCents,first.Snapshot.TotalCents+second.Snapshot.TotalCents);
        foreach(var milestone in quote.Snapshot.Milestones)
            Assert.Equal(milestone.Cents,new[]{first,second}.SelectMany(i=>i.Snapshot.Milestones).Where(m=>m.Kind==milestone.Kind).Sum(m=>m.Cents));
    }

    [Fact]
    public async Task CompetingConversionsCannotBillTheSameSourceTwice()
    {
        using var f=new BusinessFixture();
        var quote=Quote(f);
        var book=new InvoiceBook(f.Store);
        var drafts=new[]{book.SaveDraft(f.Owner,Draft(f,quote)),book.SaveDraft(f.Owner,Draft(f,quote))};
        var results=await Task.WhenAll(drafts.Select(d=>Task.Run(()=>
        {
            try{book.Issue(f.Owner,d.Id,d.Revision,BusinessFixture.Phrase);return true;}
            catch(ArgumentException){return false;}
        })));
        Assert.Single(results,r=>r);
        Assert.Single(new QuoteBook(f.Store).Versions(f.Owner),v=>v.Snapshot.Kind==DocumentKind.Invoice);
    }

    [Fact]
    public void RepeatedFractionalPortionsCannotAllocateMoreThanTheSourceCents()
    {
        using var f=new BusinessFixture();
        var payer=new CustomerBook(f.Store).Save(f.Owner,new Customer{FullName="Fictional tiny-charge payer"});
        var booking=BookingRules.AddCharge(new Booking{Name="Tiny example",PrimaryCustomerId=payer.Id,HasTrial=false,TrialCents=0,CustomDepositCents=0},"Fictional service","Custom",1,2);
        booking=new BookingBook(f.Store).Save(f.Owner,booking with{Charges=booking.Charges.Select(c=>c with{TaxRate=0m}).ToArray()});
        var quote=Quote(f,booking);
        long total=0;
        foreach(decimal quantity in new[]{0.3m,0.3m,0.3m,0.1m})
            total+=Issue(f,Draft(f,quote,[new(quote.Snapshot.Lines.Single().ChargeId,quantity)])).Snapshot.TotalCents;
        Assert.Equal(2,total);
    }

    [Fact]
    public void AcceptedAmendmentCannotRepriceChargesThatWereAlreadyBilled()
    {
        using var f=new BusinessFixture();
        var quote=Quote(f);
        var line=quote.Snapshot.Lines.Single(l=>l.Unit=="Person");
        Issue(f,Draft(f,quote,[new(line.ChargeId,1)]));
        var bookings=new BookingBook(f.Store);
        var booking=bookings.List(f.Owner).Single();
        booking=bookings.Save(f.Owner,booking with{Charges=booking.Charges.Select(c=>c.Id==line.ChargeId?c with{UnitCents=c.UnitCents+1}:c).ToArray()});
        var quotes=new QuoteBook(f.Store);
        var revised=quotes.SaveDraft(f.Owner,quotes.FromBooking(f.Owner,booking,quote.DocumentId));
        var proposed=quotes.Issue(f.Owner,revised.Id,revised.Revision,BusinessFixture.Phrase);
        Assert.Throws<ArgumentException>(()=>quotes.Accept(f.Owner,proposed.Id,proposed.Snapshot.IssueDate,"Fictional amendment acceptance"));
        Assert.Equal("Accepted",quotes.Versions(f.Owner).Single(v=>v.Id==quote.Id).Status);
    }
}
