using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;

namespace SarahBeauty.Storage.Tests;

public sealed class CalendarTests
{
    private static readonly DateOnly Day=new(2027,1,10);
    private static Artist Artist(BusinessFixture f,string name="Fictional calendar artist")=>new BookingBook(f.Store).SaveArtist(f.Owner,new(){Name=name});
    private static Appointment Work(string artist,string start="2027-01-10T10:00-05:00",string end="2027-01-10T11:00-05:00")=>new(Guid.NewGuid().ToString(),"Wedding",artist,DateTimeOffset.Parse(start),DateTimeOffset.Parse(end),"Fictional venue"){TimeZoneId="America/New_York"};
    private static Booking Booking(string name)=>BookingRules.Wedding(name) with{EventDate=Day,ReadyBy="12:00",Venue="Fictional event venue"};

    [Fact]
    public void CalendarUsesSelectedDisplayZoneWhileRetainingTheStatedTimeAndEventLocalDate()
    {
        using var f=new BusinessFixture();var artist=Artist(f);var book=new BookingBook(f.Store);
        var booking=Booking("Fictional west-coast event") with{EventTimeZoneId="America/Los_Angeles",ReadyBy="23:59"};
        var work=Work(artist.Id,"2027-01-10T23:00-08:00","2027-01-11T00:00-08:00") with{TimeZoneId="America/Los_Angeles",PlaceIds=[booking.Places[0].Id]};
        book.Save(f.Owner,booking with{Appointments=[work]});var calendar=new CalendarSources(f.Store);
        Assert.Empty(calendar.Load(f.Owner,Day,Day).Scheduled);
        var eastern=calendar.Load(f.Owner,Day.AddDays(1),Day.AddDays(1));var slot=eastern.Scheduled.Single();
        Assert.Equal(2,slot.Start!.Value.Hour);Assert.Equal(TimeSpan.FromHours(-5),slot.Start.Value.Offset);
        Assert.Equal(23,slot.StatedStart!.Value.Hour);Assert.Equal(TimeSpan.FromHours(-8),slot.StatedStart.Value.Offset);
        var pacific=calendar.Load(f.Owner,Day,Day,"America/Los_Angeles");Assert.Single(pacific.Scheduled);Assert.True(pacific.Events.Single().EventWorkScheduled);
        Assert.Equal(Day,pacific.Events.Single().EventDate);Assert.Equal("TEST",pacific.Mode);
    }

    [Fact]
    public void DateWindowsIncludeOvernightWorkButNotAnEntryEndingExactlyAtMidnight()
    {
        using var f=new BusinessFixture();var artist=Artist(f);var book=new BookingBook(f.Store);
        book.Save(f.Owner,Booking("Fictional boundary appointment") with{Appointments=[Work(artist.Id,"2027-01-10T23:30-05:00","2027-01-11T00:00-05:00")]});
        book.Save(f.Owner,Booking("Fictional overnight appointment") with{Appointments=[Work(artist.Id,"2027-01-10T23:30-05:00","2027-01-11T00:30-05:00") with{OverrideReason="Fictional explicit overnight planning review"}]});
        var snapshot=new CalendarSources(f.Store).Load(f.Owner,Day.AddDays(1),Day.AddDays(1));
        Assert.Equal("Fictional overnight appointment",snapshot.Scheduled.Single().BookingName);
    }

    [Fact]
    public void UnknownTimesAndDatesStaySeparateAndDoNotBecomeFictionalCalendarSlots()
    {
        using var f=new BusinessFixture();var artist=Artist(f);var book=new BookingBook(f.Store);
        book.Save(f.Owner,Booking("Fictional untimed work") with{Appointments=[new(Guid.NewGuid().ToString(),"Wedding",artist.Id,null,null,"")]});
        book.Save(f.Owner,BookingRules.Wedding("Fictional date undecided"));
        var snapshot=new CalendarSources(f.Store).Load(f.Owner,Day,Day);
        Assert.Empty(snapshot.Scheduled);var pending=snapshot.Unscheduled.Single();
        Assert.Null(pending.Start);Assert.Contains(pending.Issues,i=>i.Code=="TimeUndecided");Assert.Contains(pending.Issues,i=>i.Code=="ZoneUnconfirmed");
        Assert.False(snapshot.Events.Single().EventWorkScheduled);
        Assert.Equal("Fictional date undecided",snapshot.DateUndecided.Single().Name);
    }

    [Fact]
    public void RecipientConflictsReadinessAndOverrideDecisionsRemainIndependentlyVisible()
    {
        using var f=new BusinessFixture();var one=Artist(f,"Fictional first artist");var two=Artist(f,"Fictional second artist");var booking=Booking("Fictional assisted work") with{ReadyBy="11:00"};
        var first=Work(one.Id,end:"2027-01-10T11:30-05:00") with{PlaceIds=[booking.Places[0].Id]};
        var second=Work(two.Id,"2027-01-10T10:30-05:00") with{PlaceIds=[booking.Places[0].Id],OverrideReason="Fictional explicit assistance decision"};
        new BookingBook(f.Store).Save(f.Owner,booking with{Appointments=[first,second]});
        var snapshot=new CalendarSources(f.Store).Load(f.Owner,Day,Day);
        Assert.All(snapshot.Scheduled,s=>Assert.Contains(s.Issues,i=>i.Code=="RecipientOverlap" && i.OverrideRecorded));
        Assert.DoesNotContain(snapshot.Scheduled.SelectMany(s=>s.Issues),i=>i.Code=="ArtistOverlap");
        Assert.Contains(snapshot.Scheduled.Single(s=>s.AppointmentId==first.Id).Issues,i=>i.Code=="AfterReadyBy" && !i.OverrideRecorded);
        Assert.Equal("Fictional explicit assistance decision",snapshot.Scheduled.Single(s=>s.AppointmentId==second.Id).OverrideReason);
    }

    [Fact]
    public void CancellationAndArtistFiltersDoNotAssignCancelledWorkOrOtherArtistsEvents()
    {
        using var f=new BusinessFixture();var one=Artist(f,"Fictional first artist");var two=Artist(f,"Fictional second artist");var book=new BookingBook(f.Store);
        var cancelled=book.Save(f.Owner,Booking("Fictional cancelled calendar event") with{Appointments=[Work(one.Id)]});
        book.ChangeStatus(f.Owner,cancelled,"Cancelled","Fictional cancellation decision");
        book.Save(f.Owner,Booking("Fictional active calendar event") with{Appointments=[Work(one.Id)]});
        book.Save(f.Owner,Booking("Fictional other artist event") with{Appointments=[Work(two.Id)]});
        var calendar=new CalendarSources(f.Store);var filtered=calendar.Load(f.Owner,Day,Day,artistId:one.Id);
        Assert.Single(filtered.Scheduled);Assert.Single(filtered.Events);Assert.DoesNotContain(filtered.Scheduled.Single().Issues,i=>i.Code=="ArtistOverlap");
        var retained=calendar.Load(f.Owner,Day,Day,artistId:one.Id,includeCancelled:true);Assert.Equal(2,retained.Scheduled.Length);Assert.Single(retained.Scheduled,s=>s.Cancelled);
        Assert.Throws<ArgumentException>(()=>calendar.Load(f.Owner,Day,Day,artistId:"foreign-artist"));
        using var foreign=new BusinessFixture();Assert.Throws<UnauthorizedAccessException>(()=>calendar.Load(foreign.Owner,Day,Day));
    }

    [Fact]
    public void SnapshotDoesNotMixBookingAndCustomerGenerationsOrExposePrivateWorkNotes()
    {
        using var f=new BusinessFixture();f.Files.Execute("PRAGMA journal_mode=WAL;");var artist=Artist(f);var customerBook=new CustomerBook(f.Store);
        var customer=customerBook.Save(f.Owner,new(){FullName="Fictional original recipient",PrivateNotes="PRIVATE-CUSTOMER"});
        var booking=Booking("Fictional coherent calendar") with{PrivateNotes="PRIVATE-BOOKING"};
        booking=booking with{Places=[booking.Places[0] with{CustomerId=customer.Id}],Appointments=[Work(artist.Id) with{PlaceIds=[booking.Places[0].Id],PrivateNotes="PRIVATE-APPOINTMENT",ProductsAndShades="PRIVATE-PRODUCTS"}]};
        new BookingBook(f.Store).Save(f.Owner,booking);
        var calendar=new CalendarSources(f.Store){AfterBookingsRead=()=>customerBook.Save(f.Owner,customer with{FullName="Fictional changed recipient"})};
        var snapshot=calendar.Load(f.Owner,Day,Day);
        Assert.Equal("Fictional original recipient",snapshot.Scheduled.Single().Recipients.Single().Name);
        Assert.DoesNotContain("PRIVATE-",JsonSerializer.Serialize(snapshot));calendar.AfterBookingsRead=null;
        var newer=calendar.Load(f.Owner,Day,Day);Assert.Equal("Fictional changed recipient",newer.Scheduled.Single().Recipients.Single().Name);Assert.True(newer.Watermark>snapshot.Watermark);
    }

    [Fact]
    public void ConflictIndexAgreesWithAllPairsForDeterministicVariedIntervals()
    {
        var random=new Random(154);var source=new List<CalendarWork>();
        for(int i=0;i<70;i++)
        {
            var booking=Booking("Fictional indexed interval "+i);var start=new DateTimeOffset(2027,1,10,0,0,0,TimeSpan.FromHours(-5)).AddMinutes(random.Next(0,240));
            var work=new Appointment(Guid.NewGuid().ToString(),"Wedding","artist-"+random.Next(0,4),start,start.AddMinutes(random.Next(1,70)),"Fictional venue");
            source.Add(new(booking,work,["person-"+random.Next(0,12)]));
        }
        var indexed=CalendarConflicts.Build(source);
        foreach(var work in source)
        {
            var expected=source.Where(other=>other.Key!=work.Key && other.Appointment.Start<work.Appointment.End && other.Appointment.End>work.Appointment.Start
                && (other.Appointment.ArtistId==work.Appointment.ArtistId || other.People.Overlaps(work.People))).Select(other=>other.Key).ToHashSet();
            var actual=(indexed.GetValueOrDefault(work.Key)??[]).Select(i=>(i.RelatedBookingId,i.RelatedAppointmentId)).ToHashSet();
            Assert.True(expected.SetEquals(actual));
        }
    }
}
