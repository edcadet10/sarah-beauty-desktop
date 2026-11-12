using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Exports;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableDashboardUpcomingTablesTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];
    private static Booking Confirm(LedgerFixture f)
    {
        var ready=ReservationTests.Setup(f);ReservationTests.Pay(f,ready.Invoice,22500);return ReservationTests.Confirm(f,ready.Booking);
    }
    [Fact]
    public void ConfirmedGrossCommitmentHasSourceMembershipAndPlanningBalanceWithoutBecomingCashIncome()
    {
        using var f=new LedgerFixture();var booking=Confirm(f);
        var quote=f.Business.Store.Read(db=>DocumentVersions.Find(db,f.Business.Owner,id:booking.AcceptedQuoteVersionId)!);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        new BookingBook(f.Business.Store).ChangeStatus(f.Business.Owner,booking,"Cancelled","Fictional later chart cancellation");f.Business.Owner.Lock();
        var tables=ReadableDashboardUpcomingTables.Build(source,new(),new(2027,1,31),new(2027,3,1),new(2027,3,31));Assert.Equal(8,tables.Length);
        var months=tables.Single(t=>t.Name=="Dashboard upcoming monthly data");var month=Assert.Single(months.Rows);
        Assert.Equal("1",Cell(months,month,"ConfirmedCount"));Assert.Equal("30000",Cell(months,month,"KnownAgreementGrossCents"));Assert.Equal("0",Cell(months,month,"UnknownValueCount"));
        var members=tables.Single(t=>t.Name=="Dashboard upcoming month bookings");Assert.Equal(booking.Id,Cell(members,Assert.Single(members.Rows),"BookingId"));
        var bookings=tables.Single(t=>t.Name=="Dashboard upcoming booking values");Assert.Equal(quote.Hash,Cell(bookings,Assert.Single(bookings.Rows),"QuoteSha256"));
        var dues=tables.Single(t=>t.Name=="Dashboard upcoming milestone dues");Assert.Equal(7500,dues.Rows.Sum(r=>long.Parse(Cell(dues,r,"Cents"))));
        Assert.Empty(tables.Single(t=>t.Name=="Dashboard upcoming review issues").Rows);Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
        Assert.Equal(22500L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT SUM(GrossCents) FROM Movements WHERE Kind='Receipt';"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownAgreementAndUndatedArchivedArtistCasesStayExplicitWithoutPrivateNotes(bool undated)
    {
        using var f=new LedgerFixture();var booking=Confirm(f);var book=new BookingBook(f.Business.Store);const string secret="Fictional-private-upcoming-table-notes";
        booking=book.Save(f.Business.Owner,booking with{EventDate=undated?null:booking.EventDate,PrivateNotes=secret,Appointments=booking.Appointments.Select(a=>a with{PrivateNotes=secret,ProductsAndShades=secret}).ToArray()});
        if(undated){var artist=book.Artists(f.Business.Owner).Single();book.SaveArtist(f.Business.Owner,artist with{Archived=true,Notes=secret});}
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableDashboardUpcomingTables.Build(source,new(),undated?new(2027,1,31):new(2027,1,9),new(2027,3,1),new(2027,3,31));Assert.DoesNotContain(secret,JsonSerializer.Serialize(tables));
        var bookings=tables.Single(t=>t.Name=="Dashboard upcoming booking values");var row=Assert.Single(bookings.Rows);
        var months=tables.Single(t=>t.Name=="Dashboard upcoming monthly data");var month=Assert.Single(months.Rows);
        Assert.NotEmpty(tables.Single(t=>t.Name=="Dashboard upcoming review issues").Rows);
        if(undated)
        {
            Assert.Equal("Undated",Cell(bookings,row,"DateGroup"));Assert.Equal("",Cell(bookings,row,"EventDate"));Assert.Equal("0",Cell(months,month,"ConfirmedCount"));
            var artists=tables.Single(t=>t.Name=="Dashboard upcoming artists");Assert.Equal("Archived",Cell(artists,Assert.Single(artists.Rows),"StateAtCapture"));
        }
        else
        {
            Assert.Equal("Dated",Cell(bookings,row,"DateGroup"));Assert.Equal("",Cell(bookings,row,"AgreementGrossCents"));Assert.Equal("1",Cell(months,month,"UnknownValueCount"));Assert.Equal("0",Cell(months,month,"KnownAgreementGrossCents"));
        }
    }
}
