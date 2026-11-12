using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableBookingPlanningTablesTests
{
    private const string Private="Fictional-private-retained-planning-notes";
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];

    [Fact]
    public void NamedAndUnnamedPlacesAndAmbiguousHourOffsetsRemainExplicitWithoutPrivateFields()
    {
        using var f=new LedgerFixture();var book=new BookingBook(f.Business.Store);
        var artist=book.SaveArtist(f.Business.Owner,new(){Name="Fictional planning report artist",Notes=Private});
        var booking=BookingRules.Wedding("Fictional captured planning event",1) with{PrimaryCustomerId=f.CustomerId,PrivateNotes=Private};
        booking=book.Save(f.Business.Owner,booking with
        {
            Places=booking.Places.Select((p,index)=>index==0?p with{CustomerId=f.CustomerId}:p).ToArray(),
            Appointments=[new(Guid.NewGuid().ToString(),"Wedding",artist.Id,DateTimeOffset.Parse("2026-11-01T01:30:00-04:00"),DateTimeOffset.Parse("2026-11-01T01:30:00-05:00"),"Fictional venue",PrivateNotes:Private,ProductsAndShades:Private){TimeZoneId="America/New_York",PlaceIds=[booking.Places[0].Id]}]
        });
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);f.Business.Owner.Lock();
        var tables=ReadableBookingPlanningTables.Build(source,new());Assert.Equal(6,tables.Length);Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
        var facts=tables.Single(t=>t.Name=="Report booking planning facts");Assert.Equal("",Cell(facts,Assert.Single(facts.Rows),"EventDate"));
        var places=tables.Single(t=>t.Name=="Report booking recipient places");Assert.Equal(2,places.Rows.Count);
        Assert.Contains(places.Rows,r=>Cell(places,r,"NameAtCapture")=="Fictional payer");Assert.Contains(places.Rows,r=>Cell(places,r,"NameAtCapture")=="Unnamed recipient");
        var appointments=tables.Single(t=>t.Name=="Report booking appointments");var row=Assert.Single(appointments.Rows);
        Assert.Equal("-04:00:00",Cell(appointments,row,"RecordedStartOffset"));Assert.Equal("-05:00:00",Cell(appointments,row,"RecordedEndOffset"));
        Assert.Equal("2026-11-01T05:30:00.0000000+00:00",Cell(appointments,row,"StartUtc"));Assert.Equal("2026-11-01T06:30:00.0000000+00:00",Cell(appointments,row,"EndUtc"));
        Assert.Equal("Fictional planning report artist",Cell(appointments,row,"ArtistNameAtCapture"));
        var recipients=tables.Single(t=>t.Name=="Report booking appointment places");Assert.Equal(booking.Places[0].Id,Cell(recipients,Assert.Single(recipients.Rows),"PlaceId"));
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }

    [Fact]
    public void RetainedPlansKeepOriginalTrialAndStatusAfterLaterCancellationAndArchiving()
    {
        using var f=new LedgerFixture();BookPeriodTests.Ready(f);var bookings=new BookingBook(f.Business.Store);
        var booking=bookings.Save(f.Business.Owner,BookingRules.Wedding("Fictional retained original plan",1) with{PrimaryCustomerId=f.CustomerId,PrivateNotes=Private});
        var periods=new BookPeriods(f.Business.Store);var closed=BookPeriodTests.Close(f,periods);
        periods.Reopen(f.Business.Owner,closed.Id,Private,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        booking=bookings.ChangeStatus(f.Business.Owner,booking,"Cancelled",Private);bookings.Save(f.Business.Owner,booking with{Archived=true,Name="Fictional cancelled current plan"});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableBookingPlanningTables.Build(source,new());Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
        var facts=tables.Single(t=>t.Name=="Report booking planning facts");
        var retained=facts.Rows.Single(r=>r[0]==closed.Id);var current=facts.Rows.Single(r=>r[0]==source.Source.GenerationId);
        Assert.Equal("Fictional retained original plan",Cell(facts,retained,"NameAtCapture"));Assert.Equal("true",Cell(facts,retained,"HasTrialAtCapture"));Assert.Equal("false",Cell(facts,retained,"ArchivedAtCapture"));
        Assert.Equal("Cancelled",Cell(facts,current,"StatusAtCapture"));Assert.Equal("true",Cell(facts,current,"HasTrialAtCapture"));Assert.Equal("true",Cell(facts,current,"ArchivedAtCapture"));
        Assert.Single(f.Book.Posted(f.Business.Owner));
    }
}
