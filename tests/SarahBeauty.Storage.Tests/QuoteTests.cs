using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;

namespace SarahBeauty.Storage.Tests;

public sealed class QuoteTests
{
    internal static Booking Booking(BusinessFixture f)
    {
        var client=new CustomerBook(f.Store).Save(f.Owner,new Customer{FullName="Fictional client",PrivateNotes="PRIVATE-NOTES-SENTINEL",Sensitivities="PRIVATE-SENSITIVITY-SENTINEL"});
        var booking=BookingRules.Wedding("Fictional wedding",9,76.6m) with{PrimaryCustomerId=client.Id,PrivateNotes="PRIVATE-BOOKING-SENTINEL"};
        return new BookingBook(f.Store).Save(f.Owner,booking with{Charges=booking.Charges.Select(c=>c with{TaxRate=0m}).ToArray()});
    }

    [Fact]
    public void IssuanceFreezesAllowlistedDataAndRetryKeepsTheNumber()
    {
        using var f=new BusinessFixture();
        var booking=Booking(f);
        var quotes=new QuoteBook(f.Store);
        var draft=quotes.SaveDraft(f.Owner,quotes.FromBooking(f.Owner,booking) with{Terms="Fictional agreed terms"});
        var issued=quotes.Issue(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(145660,issued.Snapshot.TotalCents);
        Assert.Equal(issued.Id,quotes.Issue(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Id);
        string json=JsonSerializer.Serialize(issued.Snapshot);
        Assert.DoesNotContain("PRIVATE-",json);
        var customerBook=new CustomerBook(f.Store);
        var customer=customerBook.Search(f.Owner).Single();
        customerBook.Save(f.Owner,customer with{FullName="Changed after issuance",BillingAddress="Different address"});
        Assert.Equal("Fictional client",quotes.Versions(f.Owner).Single().Snapshot.Client.Name);
        Assert.Equal(issued.Hash,quotes.Versions(f.Owner).Single().Hash);
    }

    [Fact]
    public void AcceptanceRecordsTheExactVersionAndAProposedRevisionDoesNotEraseIt()
    {
        using var f=new BusinessFixture();
        var booking=Booking(f);
        var quotes=new QuoteBook(f.Store);
        var first=quotes.SaveDraft(f.Owner,quotes.FromBooking(f.Owner,booking));
        var issued=quotes.Issue(f.Owner,first.Id,first.Revision,BusinessFixture.Phrase);
        quotes.Accept(f.Owner,issued.Id,issued.Snapshot.IssueDate,"Fictional acceptance fixture");
        booking=new BookingBook(f.Store).List(f.Owner).Single();
        Assert.Equal(issued.Id,booking.AcceptedQuoteVersionId);
        Assert.Equal("Awaiting deposit",booking.Status);
        var revised=quotes.SaveDraft(f.Owner,quotes.FromBooking(f.Owner,booking,issued.DocumentId) with{Terms="Revised proposed terms"});
        var second=quotes.Issue(f.Owner,revised.Id,revised.Revision,BusinessFixture.Phrase);
        Assert.Equal(issued.Snapshot.Number,second.Snapshot.Number);
        Assert.Equal(2,second.Snapshot.Version);
        Assert.Equal("Accepted",quotes.Versions(f.Owner).Single(v=>v.Id==issued.Id).Status);
        quotes.Accept(f.Owner,second.Id,second.Snapshot.IssueDate,"Fictional revised acceptance");
        Assert.Equal("Superseded",quotes.Versions(f.Owner).Single(v=>v.Id==issued.Id).Status);
        Assert.Equal(second.Id,new BookingBook(f.Store).List(f.Owner).Single().AcceptedQuoteVersionId);
    }

    [Fact]
    public void ChangedBookingAndUnknownTaxBlockIssuanceWithoutConsumingANumber()
    {
        using var f=new BusinessFixture();
        var booking=Booking(f);
        var book=new BookingBook(f.Store);
        var quotes=new QuoteBook(f.Store);
        var draft=quotes.SaveDraft(f.Owner,quotes.FromBooking(f.Owner,booking));
        booking=book.Save(f.Owner,booking with{Venue="Changed venue"});
        Assert.Throws<ArgumentException>(()=>quotes.Issue(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Empty(quotes.Versions(f.Owner));
        Assert.Equal(0L,TestStore.Scalar(f.Files.DatabasePath,"SELECT COUNT(*) FROM DocumentCounters;"));
        booking=book.Save(f.Owner,booking with{Charges=booking.Charges.Select(c=>c with{TaxRate=(decimal?)null}).ToArray()});
        draft=quotes.SaveDraft(f.Owner,draft with{BookingRevision=booking.Revision});
        Assert.Throws<ArgumentException>(()=>quotes.Issue(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
    }
}
