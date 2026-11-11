using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;

namespace SarahBeauty.Storage.Tests;

public sealed class AppointmentTests
{
    private static Appointment Work(string artist,string[]? places=null)=>new(Guid.NewGuid().ToString(),"Wedding",artist,DateTimeOffset.Parse("2027-01-10T10:00:00-05:00"),DateTimeOffset.Parse("2027-01-10T11:00:00-05:00"),"Fictional venue"){PlaceIds=places??[]};

    [Fact]
    public void ARecipientCannotBeScheduledWithTwoArtistsAtOnceWithoutAnExplicitOverride()
    {
        using var f=new BusinessFixture();var book=new BookingBook(f.Store);
        var one=book.SaveArtist(f.Owner,new(){Name="Fictional first artist"});var two=book.SaveArtist(f.Owner,new(){Name="Fictional second artist"});
        var booking=BookingRules.Wedding("Fictional shared recipient",1);string place=booking.Places[0].Id;
        booking=book.Save(f.Owner,booking with{Appointments=[Work(one.Id,[place])]});
        var second=Work(two.Id,[place]);
        Assert.Contains("recipient",Assert.Throws<ArgumentException>(()=>book.Save(f.Owner,booking with{Appointments=[..booking.Appointments,second]})).Message);
        var saved=book.Save(f.Owner,booking with{Appointments=[..booking.Appointments,second with{OverrideReason="Fictional explicit assisted-work decision"}]});
        Assert.Equal(2,saved.Appointments.Length);Assert.Equal(2,saved.Places.Length);
    }

    [Fact]
    public void NamedRecipientIdentityIsComparedAcrossBookingsButEqualNamesDoNotMergeCustomers()
    {
        using var f=new BusinessFixture();var book=new BookingBook(f.Store);var customers=new CustomerBook(f.Store);
        var customer=customers.Save(f.Owner,new(){FullName="Fictional same name"});var distinct=customers.Save(f.Owner,new(){FullName="Fictional same name"});
        var one=book.SaveArtist(f.Owner,new(){Name="Fictional first artist"});var two=book.SaveArtist(f.Owner,new(){Name="Fictional second artist"});
        var first=BookingRules.Wedding("Fictional first recipient event");first=first with{Places=[first.Places[0] with{CustomerId=customer.Id}],Appointments=[Work(one.Id,[first.Places[0].Id]) with{OverrideReason="Fictional original event review"}]};book.Save(f.Owner,first);
        var second=BookingRules.Wedding("Fictional second recipient event");second=second with{Places=[second.Places[0] with{CustomerId=customer.Id}],Appointments=[Work(two.Id,[second.Places[0].Id])]};
        Assert.Throws<ArgumentException>(()=>book.Save(f.Owner,second));
        second=second with{Places=[second.Places[0] with{CustomerId=distinct.Id}]};
        Assert.Single(book.Save(f.Owner,second).Appointments);
    }

    [Fact]
    public void RecipientsRequireDistinctOwnedPlacesAndRetiredPlacesRemainOnlyOnCancelledWork()
    {
        using var f=new BusinessFixture();var book=new BookingBook(f.Store);var artist=book.SaveArtist(f.Owner,new(){Name="Fictional artist"});
        var booking=BookingRules.Wedding("Fictional recipient validation",2);string guest=booking.Charges.Single(c=>c.Unit=="Person").Id;
        string retired=booking.Places.Last().Id;booking=BookingRules.ChangeQuantity(booking,guest,1);
        foreach(var ids in new[]{new[]{"foreign-place"},new[]{booking.Places[0].Id,booking.Places[0].Id},new[]{retired}})
            Assert.Throws<ArgumentException>(()=>book.Save(f.Owner,booking with{Appointments=[Work(artist.Id,ids)]}));
        var saved=book.Save(f.Owner,booking with{Appointments=[Work(artist.Id,[retired]) with{Status="Cancelled"}]});
        Assert.Equal(retired,saved.Appointments.Single().PlaceIds.Single());
    }

    [Fact]
    public void PlannedEndUsesCopiedDurationsAndNeverAssumesAnUnknownOrNoTrialDuration()
    {
        var booking=BookingRules.Wedding("Fictional group duration",2);var guests=booking.Places.Where(p=>p.ChargeId==booking.Charges.Single(c=>c.Unit=="Person").Id).Select(p=>p.Id).ToArray();
        booking=booking with{Charges=booking.Charges.Select(c=>c.Unit=="Person"?c with{DurationMinutes=45}:c).ToArray(),TrialDurationMinutes=60};
        var work=Work("fictional-artist",guests);
        Assert.Equal(90,AppointmentPlanning.DurationMinutes(booking,work));Assert.Equal(work.Start!.Value.AddMinutes(90),AppointmentPlanning.EndFromDurations(booking,work));
        Assert.Null(AppointmentPlanning.DurationMinutes(booking,work with{PlaceIds=[booking.Places[0].Id]}));
        Assert.Throws<ArgumentException>(()=>AppointmentPlanning.EndFromDurations(booking,work with{PlaceIds=[]}));
        Assert.Equal(60,AppointmentPlanning.DurationMinutes(booking,work with{Kind="Trial",PlaceIds=[booking.Places[0].Id]}));
        Assert.Null(AppointmentPlanning.DurationMinutes(booking with{HasTrial=false},work with{Kind="Trial",PlaceIds=[]}));
    }

    [Fact]
    public void CompletedAndCancelledWorkRetainsItsArchivedArtistWhileNewWorkNeedsAnActiveArtist()
    {
        using var f=new BusinessFixture();var book=new BookingBook(f.Store);var artist=book.SaveArtist(f.Owner,new(){Name="Fictional former artist"});
        book.SaveArtist(f.Owner,artist with{Archived=true});
        Assert.Throws<ArgumentException>(()=>book.Save(f.Owner,BookingRules.Wedding("Fictional planned work") with{Appointments=[Work(artist.Id)]}));
        Assert.Single(book.Save(f.Owner,BookingRules.Wedding("Fictional completed work") with{Appointments=[Work(artist.Id) with{Status="Completed"}]}).Appointments);
    }

    [Fact]
    public void ACancelledBookingRetainsAppointmentsButDoesNotReserveAnArtistsTime()
    {
        using var f=new BusinessFixture();var book=new BookingBook(f.Store);var artist=book.SaveArtist(f.Owner,new(){Name="Fictional artist"});
        var appointment=new Appointment(Guid.NewGuid().ToString(),"Wedding",artist.Id,DateTimeOffset.Parse("2027-01-10T10:00:00-05:00"),DateTimeOffset.Parse("2027-01-10T11:00:00-05:00"),"Fictional venue");
        var original=book.Save(f.Owner,BookingRules.Wedding("Fictional cancelled event") with{Appointments=[appointment]});
        original=book.ChangeStatus(f.Owner,original,"Cancelled","Fictional recorded cancellation");
        var next=book.Save(f.Owner,BookingRules.Wedding("Fictional later event") with{Appointments=[appointment with{Id=Guid.NewGuid().ToString()}]});
        Assert.Single(next.Appointments);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(appointment),System.Text.Json.JsonSerializer.Serialize(book.List(f.Owner).Single(b=>b.Id==original.Id).Appointments.Single()));
    }
}
