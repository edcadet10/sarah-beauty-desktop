using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Dashboard;
using SarahBeauty.Business.Exports;

namespace SarahBeauty.Storage.Tests;

public sealed class CapturedDashboardUpcomingTests
{
    [Fact]
    public void CapturedConfirmedCommitmentMatchesHumanViewAfterLaterCancellationAndLock()
    {
        using var f=new LedgerFixture();var ready=ReservationTests.Setup(f);ReservationTests.Pay(f,ready.Invoice,22500);
        var booking=ReservationTests.Confirm(f,ready.Booking);var books=new BookingBook(f.Business.Store);
        const string secret="Fictional-private-upcoming-capture-notes";
        booking=books.Save(f.Business.Owner,booking with{PrivateNotes=secret,Appointments=booking.Appointments.Select(a=>a with{PrivateNotes=secret,ProductsAndShades=secret}).ToArray()});
        var expected=new DashboardUpcomingSources(f.Business.Store).Load(f.Business.Owner,new(2027,1,31),new(2027,3,1),new(2027,3,31));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        books.ChangeStatus(f.Business.Owner,booking,"Cancelled","Fictional later cancellation");f.Business.Owner.Lock();
        var actual=source.Read(db=>DashboardUpcomingSources.Read(db,source.Source.BusinessId,new(2027,1,31),new(2027,3,1),new(2027,3,31),expected.GeneratedAtUtc));
        Assert.Equal(JsonSerializer.Serialize(expected),JsonSerializer.Serialize(actual));Assert.DoesNotContain(secret,JsonSerializer.Serialize(actual));
        var row=Assert.Single(actual.Bookings);Assert.Equal(30000,row.AgreementGrossCents);Assert.Empty(row.ReviewIssues);
        Assert.Equal(7500,row.Billing.Dues.Sum(d=>d.Cents));Assert.Equal(1,actual.Months.Single().Count);
        Assert.Equal(30000,actual.Months[0].KnownAgreementGrossCents);Assert.Equal(source.Source.Watermark,actual.Watermark);
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }

    [Fact]
    public void EarlierPlanningDateKeepsUnknownValueAndLateHumanLockIsRejected()
    {
        using var f=new LedgerFixture();var ready=ReservationTests.Setup(f);ReservationTests.Pay(f,ready.Invoice,22500);ReservationTests.Confirm(f,ready.Booking);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var human=new DashboardUpcomingSources(f.Business.Store){AfterBookingsRead=()=>f.Business.Owner.Lock()};
        Assert.Throws<UnauthorizedAccessException>(()=>human.Load(f.Business.Owner,new(2027,1,31),new(2027,3,1),new(2027,3,31)));
        var actual=source.Read(db=>DashboardUpcomingSources.Read(db,source.Source.BusinessId,new(2027,1,9),new(2027,3,1),new(2027,3,31),source.Source.CapturedAtUtc));
        Assert.Null(Assert.Single(actual.Bookings).AgreementGrossCents);Assert.Equal(1,actual.Months.Single().UnknownValueCount);Assert.Equal(0,actual.Months[0].KnownAgreementGrossCents);
        Assert.Contains(actual.Bookings[0].ReviewIssues,i=>i.Contains("acceptance"));Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
        Assert.Throws<ArgumentException>(()=>source.Read(db=>DashboardUpcomingSources.Read(db,source.Source.BusinessId,new(2027,1,31),new(2027,1,1),new(2027,3,31),source.Source.CapturedAtUtc)));
        Assert.Throws<ArgumentException>(()=>source.Read(db=>DashboardUpcomingSources.Read(db,Guid.NewGuid().ToString(),new(2027,1,31),new(2027,3,1),new(2027,3,31),source.Source.CapturedAtUtc)));
    }
}
