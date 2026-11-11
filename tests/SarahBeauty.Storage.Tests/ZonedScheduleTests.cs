using System.Text.Json.Nodes;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Rules;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace SarahBeauty.Storage.Tests;

public sealed class ZonedScheduleTests
{
    [Theory]
    [InlineData("-04:00",5)]
    [InlineData("-05:00",6)]
    public void RepeatedLocalTimesRequireAndRetainTheChosenOffset(string offset,int utcHour)
    {
        var actual=ZonedTime.Resolve(new(2007,11,4),"01:30","America/New_York",offset);
        Assert.Equal(new DateTimeOffset(2007,11,4,utcHour,30,0,TimeSpan.Zero),actual.ToUniversalTime());
        ZonedTime.Validate(actual,"America/New_York");
    }

    [Fact]
    public void InvalidOrUnresolvedClockChangesWrongOffsetsAndUnknownZonesAreRejected()
    {
        Assert.Throws<ArgumentException>(()=>ZonedTime.Resolve(new(2007,3,11),"02:30","America/New_York","-05:00"));
        Assert.Throws<ArgumentException>(()=>ZonedTime.Resolve(new(2007,11,4),"01:30","America/New_York"));
        Assert.Throws<ArgumentException>(()=>ZonedTime.Resolve(new(2007,7,1),"12:00","America/New_York","-05:00"));
        Assert.Throws<ArgumentException>(()=>ZonedTime.Resolve(new(2007,1,1),"24:00","America/New_York"));
        Assert.Throws<ArgumentException>(()=>ZonedTime.Resolve(new(2007,1,1),"12:00","fictional/unknown-zone"));
        Assert.Throws<ArgumentException>(()=>ZonedTime.Resolve(new(2007,1,1),"12:00","America/New_York","+15:00"));
    }

    [Theory]
    [InlineData("2007-03-11T01:30:00-05:00","2007-03-11T03:30:00-04:00")]
    [InlineData("2007-11-04T01:30:00-04:00","2007-11-04T01:30:00-05:00")]
    public void DurationPlanningUsesElapsedTimeAcrossBothClockChanges(string start,string expectedEnd)
    {
        var booking=BookingRules.Wedding("Fictional clock-change planning") with{TrialDurationMinutes=60};
        var appointment=new Appointment("fictional-appointment","Trial","fictional-artist",DateTimeOffset.Parse(start),null,"Fictional location"){TimeZoneId="America/New_York"};
        var end=AppointmentPlanning.EndFromDurations(booking,appointment);
        Assert.Equal(DateTimeOffset.Parse(expectedEnd),end);
        Assert.Equal(DateTimeOffset.Parse(expectedEnd).Offset,end.Offset);
        Assert.Equal(TimeSpan.FromHours(1),end-appointment.Start!.Value);
    }

    [Fact]
    public void AppointmentZonesValidateOffsetsWhileLegacyOffsetOnlyRecordsStayUnconfirmed()
    {
        using var f=new BusinessFixture();var book=new BookingBook(f.Store);var artist=book.SaveArtist(f.Owner,new(){Name="Fictional zoned artist"});
        var appointment=new Appointment(Guid.NewGuid().ToString(),"Wedding",artist.Id,DateTimeOffset.Parse("2027-01-10T10:00:00+00:00"),DateTimeOffset.Parse("2027-01-10T11:00:00+00:00"),"Fictional location");
        Assert.Throws<ArgumentException>(()=>book.Save(f.Owner,BookingRules.Wedding("Fictional wrong zone") with{Appointments=[appointment with{TimeZoneId="America/New_York"}]}));
        var stored=book.Save(f.Owner,BookingRules.Wedding("Fictional offset-only legacy record") with{Appointments=[appointment]});
        var retained=book.List(f.Owner).Single(b=>b.Id==stored.Id).Appointments.Single();
        Assert.Equal("",retained.TimeZoneId);Assert.Equal(TimeSpan.Zero,retained.Start!.Value.Offset);
    }

    [Fact]
    public void ReadyByAmbiguityRequiresAnOffsetAndDoesNotInventADeadline()
    {
        using var f=new BusinessFixture();var book=new BookingBook(f.Store);
        var booking=BookingRules.Wedding("Fictional repeated ready time") with{EventDate=new(2007,11,4),ReadyBy="01:30"};
        Assert.Throws<ArgumentException>(()=>book.Save(f.Owner,booking));
        var first=book.Save(f.Owner,booking with{ReadyByUtcOffset="-04:00"});
        var second=book.Save(f.Owner,first with{ReadyByUtcOffset="-05:00"});
        Assert.Equal(TimeSpan.FromHours(1),BookingSchedule.ReadyBy(second)!.Value-BookingSchedule.ReadyBy(first)!.Value);
        Assert.Throws<ArgumentException>(()=>book.Save(f.Owner,second with{ReadyBy="",ReadyByUtcOffset="-05:00"}));
        Assert.Throws<ArgumentException>(()=>book.Save(f.Owner,second with{EventTimeZoneId="fictional/unknown-zone"}));
    }

    [Fact]
    public void NonHourOffsetsUseTheSelectedZonesActualOffset()
    {
        var result=ZonedTime.Resolve(new(2027,1,10),"12:00","Asia/Kathmandu","+05:45");
        Assert.Equal(TimeSpan.FromMinutes(345),result.Offset);
        ZonedTime.Validate(result,"Asia/Kathmandu");
        Assert.Throws<ArgumentException>(()=>ZonedTime.Validate(result,"America/New_York"));
    }

    [Fact]
    public void IssuedQuotesRetainTheEventZoneAndAZoneChangeNeedsARevisedAgreement()
    {
        using var f=new BusinessFixture();var bookings=new BookingBook(f.Store);
        var booking=bookings.Save(f.Owner,QuoteTests.Booking(f) with{EventDate=new(2027,1,10),ReadyBy="12:00",EventTimeZoneId="America/Los_Angeles",ReadyByUtcOffset="-08:00"});
        var quotes=new QuoteBook(f.Store);var draft=quotes.SaveDraft(f.Owner,quotes.FromBooking(f.Owner,booking));
        var issued=quotes.Issue(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        var accepted=quotes.Accept(f.Owner,issued.Id,issued.Snapshot.IssueDate,"Fictional zoned event agreement");
        booking=bookings.List(f.Owner).Single();
        booking=bookings.Save(f.Owner,booking with{EventTimeZoneId="America/New_York",ReadyByUtcOffset="-05:00"});
        Assert.Contains("event",Assert.Throws<ArgumentException>(()=>ReservationRules.ValidateAgreement(booking,accepted,accepted.Snapshot.IssueDate)).Message);
        var retained=quotes.Versions(f.Owner).Single();Assert.Equal(issued.Hash,retained.Hash);Assert.Equal("America/Los_Angeles",retained.Snapshot.Event.TimeZoneId);
        using var pdf=PdfDocument.Open(new DocumentArtifacts(f.Store).Render(f.Owner,issued.Id));
        string text=string.Join('\n',pdf.GetPages().Select(p=>ContentOrderTextExtractor.GetText(p)));
        Assert.Contains("America/Los_Angeles",text);Assert.Contains("UTC-08:00",text);Assert.DoesNotContain("(Eastern Time)",text);
    }

    [Fact]
    public void LegacyEventJsonKeepsItsOriginalHashAndKnownEasternLabelWhenRendered()
    {
        using var f=new BusinessFixture();var booking=new BookingBook(f.Store).Save(f.Owner,QuoteTests.Booking(f) with{ReadyBy="12:00"});
        var quotes=new QuoteBook(f.Store);var draft=quotes.SaveDraft(f.Owner,quotes.FromBooking(f.Owner,booking));var issued=quotes.Issue(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        string raw=(string)TestStore.Scalar(f.Files.DatabasePath,"SELECT DataJson FROM DocumentVersions;")!;var node=JsonNode.Parse(raw)!;
        node["Event"]!.AsObject().Remove("TimeZoneId");node["Event"]!.AsObject().Remove("ReadyByUtcOffset");string legacy=node.ToJsonString();
        string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(legacy)));
        f.Store.Transaction(db=>{db.Execute("DROP TRIGGER DocumentVersions_Immutable;");db.Execute("UPDATE DocumentVersions SET DataJson=$json,SnapshotHash=$hash WHERE Id=$id;",("$json",legacy),("$hash",hash),("$id",issued.Id));return true;});
        Assert.Equal(hash,quotes.Versions(f.Owner).Single().Hash);
        using var pdf=PdfDocument.Open(new DocumentArtifacts(f.Store).Render(f.Owner,issued.Id));
        Assert.Contains("(Eastern Time)",string.Join('\n',pdf.GetPages().Select(p=>ContentOrderTextExtractor.GetText(p))));
        Assert.Equal(hash,quotes.Versions(f.Owner).Single().Hash);
    }
}
