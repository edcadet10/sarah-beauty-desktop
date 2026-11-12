using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Exports;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableScheduleTablesTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];
    [Fact]
    public void AppointmentInstantsOriginalOffsetsRecipientsAndCancelledParentStateRemainVisibleWithoutPrivateNotes()
    {
        using var f=new BusinessFixture();const string secret="Fictional-private-schedule-sentinel";var book=new BookingBook(f.Store);var artist=book.SaveArtist(f.Owner,new(){Name="Fictional readable artist",Notes=secret});var booking=BookingRules.Wedding("Fictional ambiguous-hour event",1);var place=booking.Places[0].Id;
        var appointment=new Appointment(Guid.NewGuid().ToString(),"Wedding",artist.Id,DateTimeOffset.Parse("2026-11-01T01:30:00-04:00"),DateTimeOffset.Parse("2026-11-01T01:30:00-05:00"),"Fictional venue",OverrideReason:secret,ProductsAndShades:secret,PrivateNotes:secret){PlaceIds=[place],TimeZoneId="America/New_York"};booking=book.Save(f.Owner,booking with{EventDate=new(2026,11,1),PrivateNotes=secret,Appointments=[appointment]});book.ChangeStatus(f.Owner,booking,"Cancelled",secret);
        using var source=new ReadableExportSnapshots(f.Store).Capture(f.Owner);var tables=ReadableScheduleTables.Build(source,new());Assert.Equal(5,tables.Length);Assert.DoesNotContain(secret,JsonSerializer.Serialize(tables));var appointments=tables.Single(t=>t.Name=="Appointments");var row=Assert.Single(appointments.Rows);Assert.Equal("Cancelled",Cell(appointments,row,"BookingStatus"));Assert.Equal("Planned",Cell(appointments,row,"Status"));Assert.StartsWith("2026-11-01T05:30:00",Cell(appointments,row,"StartUtc"));Assert.StartsWith("2026-11-01T06:30:00",Cell(appointments,row,"EndUtc"));Assert.EndsWith("-04:00",Cell(appointments,row,"RecordedStartWithOffset"));Assert.EndsWith("-05:00",Cell(appointments,row,"RecordedEndWithOffset"));Assert.Equal("America/New_York",Cell(appointments,row,"TimeZoneId"));Assert.Equal(place,tables.Single(t=>t.Name=="Appointment places").Rows.Single()[3]);Assert.Equal("Cancelled",tables.Single(t=>t.Name=="Booking status history").Rows.Single()[4]);
    }
    [Fact]
    public void ActualApprovedDepositAndReservationExportTheirExactAgreementAndAmounts()
    {
        using var f=new LedgerFixture();var (booking,invoice)=ReservationTests.Setup(f);ReservationTests.Pay(f,invoice,22500);const string secret="Fictional-private-reservation-reason";var confirmed=new BookingReservations(f.Business.Store).Confirm(f.Business.Owner,booking.Id,booking.Revision,Guid.NewGuid().ToString(),f.Date,secret,BusinessFixture.Phrase);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableScheduleTables.Build(source,new());Assert.DoesNotContain(secret,JsonSerializer.Serialize(tables));var reservations=tables.Single(t=>t.Name=="Booking reservations");var row=Assert.Single(reservations.Rows);Assert.Equal(confirmed.Reservations.Single().RequestId,Cell(reservations,row,"RequestId"));Assert.Equal(confirmed.AcceptedQuoteVersionId,Cell(reservations,row,"QuoteVersionId"));Assert.Equal("15000",Cell(reservations,row,"TrialCents"));Assert.Equal("7500",Cell(reservations,row,"DepositCents"));Assert.Equal("2027-01-10",Cell(reservations,row,"Date"));Assert.Contains(tables.Single(t=>t.Name=="Booking status history").Rows,r=>r[4]=="Confirmed");
    }
    [Fact]
    public void UnknownAppointmentTimesStayBlankAndArchivedArtistsRemainRetained()
    {
        using var f=new BusinessFixture();var book=new BookingBook(f.Store);var artist=book.SaveArtist(f.Owner,new(){Name="Fictional retained artist"});var booking=BookingRules.Wedding("Fictional unplanned time");book.Save(f.Owner,booking with{Appointments=[new(Guid.NewGuid().ToString(),"Wedding",artist.Id,null,null,"Fictional location")]});book.SaveArtist(f.Owner,artist with{Archived=true});
        using var source=new ReadableExportSnapshots(f.Store).Capture(f.Owner);var tables=ReadableScheduleTables.Build(source,new());var appointments=tables.Single(t=>t.Name=="Appointments");var row=Assert.Single(appointments.Rows);Assert.Equal("",Cell(appointments,row,"StartUtc"));Assert.Equal("",Cell(appointments,row,"EndUtc"));Assert.Equal("",Cell(appointments,row,"RecordedStartWithOffset"));Assert.Equal("true",tables.Single(t=>t.Name=="Artists").Rows.Single()[4]);
    }
}
