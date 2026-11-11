using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Dashboard;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class DashboardUpcomingTests
{
    private static (Booking Booking,DocumentVersion Invoice) Confirmed(LedgerFixture f)
    {
        var ready=ReservationTests.Setup(f);ReservationTests.Pay(f,ready.Invoice,22500);
        return(ReservationTests.Confirm(f,ready.Booking),ready.Invoice);
    }
    private static DashboardUpcomingSnapshot Read(LedgerFixture f,DateOnly? asOf=null)=>new DashboardUpcomingSources(f.Business.Store)
        .Load(f.Business.Owner,asOf??new(2027,1,31),new(2027,3,1),new(2027,3,31));

    [Fact]
    public void ConfirmedAgreementValueIsASeparateCommitmentAndUnacceptedPricesDoNotInflateIt()
    {
        using var f=new LedgerFixture();var (booking,_)=Confirmed(f);var snapshot=Read(f);var row=Assert.Single(snapshot.Bookings);
        Assert.Equal(30000,row.AgreementGrossCents);Assert.Empty(row.ReviewIssues);Assert.Equal(7500,row.Billing.Dues.Sum(d=>d.Cents));
        Assert.Equal(30000,Assert.Single(snapshot.Months).KnownAgreementGrossCents);Assert.Equal(1,snapshot.Months[0].Count);Assert.Equal(0,snapshot.Months[0].UnknownValueCount);
        Assert.Equal(-22500,f.Balance("4000"));Assert.Single(f.Book.Posted(f.Business.Owner));
        new BookingBook(f.Business.Store).Save(f.Business.Owner,booking with{Charges=booking.Charges.Select(c=>c with{UnitCents=45000}).ToArray()});
        var changed=Assert.Single(Read(f).Bookings);Assert.Equal(30000,changed.AgreementGrossCents);Assert.Contains(changed.ReviewIssues,i=>i.Contains("changed"));
        var before=Assert.Single(Read(f,new(2027,1,9)).Bookings);Assert.Null(before.AgreementGrossCents);Assert.Contains(before.ReviewIssues,i=>i.Contains("acceptance"));
    }

    [Fact]
    public void CreditAdjustmentNeedsCommitmentReviewUntilTheSameAgreementIsFullyReissued()
    {
        using var f=new LedgerFixture();var (booking,invoice)=Confirmed(f);var credits=new CreditNoteBook(f.Business.Store);
        var draft=credits.SaveDraft(f.Business.Owner,new CreditNoteDraft{Name="Fictional partial commitment reduction",InvoiceVersionId=invoice.Id,IssueDate=f.Date.AddDays(1),
            Portions=[new(invoice.Snapshot.Lines.Single().ChargeId,0.1m)]});credits.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        var adjusted=Read(f);Assert.Null(Assert.Single(adjusted.Bookings).AgreementGrossCents);Assert.Equal(1,adjusted.Months[0].UnknownValueCount);Assert.Equal(0,adjusted.Months[0].KnownAgreementGrossCents);
        var invoices=new InvoiceBook(f.Business.Store);var replacement=invoices.SaveDraft(f.Business.Owner,new InvoiceDraft{Name="Fictional reissued source portion",QuoteVersionId=booking.AcceptedQuoteVersionId,
            PayerCustomerId=f.CustomerId,IssueDate=f.Date.AddDays(2),Portions=[new(invoice.Snapshot.Lines.Single().ChargeId,0.1m)]});
        invoices.Issue(f.Business.Owner,replacement.Id,replacement.Revision,BusinessFixture.Phrase);
        var reissued=Read(f);Assert.Equal(30000,Assert.Single(reissued.Bookings).AgreementGrossCents);Assert.Equal(0,reissued.Months[0].UnknownValueCount);
        Assert.Equal(7500,reissued.Bookings[0].Billing.Dues.Sum(d=>d.Cents));Assert.Single(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void CancelledCompletedArchivedAndOutOfWindowWorkDoNotEnterTheConfirmedWindow()
    {
        using var f=new LedgerFixture();var (booking,_)=Confirmed(f);var sources=new DashboardUpcomingSources(f.Business.Store);
        Assert.Empty(sources.Load(f.Business.Owner,new(2027,1,31),new(2027,2,1),new(2027,2,28)).Bookings);
        var books=new BookingBook(f.Business.Store);var archived=books.Save(f.Business.Owner,booking with{Archived=true});Assert.Empty(Read(f).Bookings);
        booking=books.Save(f.Business.Owner,archived with{Archived=false});booking=books.ChangeStatus(f.Business.Owner,booking,"Completed","Fictional completed work");Assert.Empty(Read(f).Bookings);
        books.ChangeStatus(f.Business.Owner,booking,"Cancelled","Fictional later status-only correction");Assert.Empty(Read(f).Bookings);
        Assert.Equal(7500,new PaymentApplications(f.Business.Store).Outstanding(f.Business.Owner,new(2027,1,31)).Sum(d=>d.Cents));
    }

    [Fact]
    public void UndatedWorkAndArchivedArtistsRemainExplicitAndPrivateWorkNotesAreExcluded()
    {
        using var f=new LedgerFixture();var (booking,_)=Confirmed(f);var books=new BookingBook(f.Business.Store);var artist=books.Artists(f.Business.Owner).Single();
        booking=books.Save(f.Business.Owner,booking with{EventDate=null,PrivateNotes="PRIVATE-UPCOMING",Appointments=booking.Appointments.Select(a=>a with{PrivateNotes="PRIVATE-WORK",ProductsAndShades="PRIVATE-PRODUCTS"}).ToArray()});
        books.SaveArtist(f.Business.Owner,artist with{Archived=true,Notes="PRIVATE-ARTIST"});var snapshot=Read(f);
        Assert.Empty(snapshot.Bookings);Assert.Equal(0,snapshot.Months[0].Count);var row=Assert.Single(snapshot.Undated);Assert.Null(row.EventDate);
        Assert.Equal("Archived",Assert.Single(row.Artists).State);Assert.Contains(row.ReviewIssues,i=>i.Contains("artist"));Assert.Contains(row.ReviewIssues,i=>i.Contains("event date"));
        Assert.DoesNotContain("PRIVATE-",JsonSerializer.Serialize(snapshot));
    }

    [Fact]
    public void ConcurrentCancellationCannotMixBookingStateAgreementBalancesOrWatermark()
    {
        using var f=new LedgerFixture();f.Business.Files.Execute("PRAGMA journal_mode=WAL;");var (booking,_)=Confirmed(f);
        long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        var sources=new DashboardUpcomingSources(f.Business.Store){AfterBookingsRead=()=>new BookingBook(f.Business.Store).ChangeStatus(f.Business.Owner,booking,"Cancelled","Fictional concurrent cancellation")};
        var snapshot=sources.Load(f.Business.Owner,new(2027,1,31),new(2027,3,1),new(2027,3,31));Assert.Equal(watermark,snapshot.Watermark);
        var row=Assert.Single(snapshot.Bookings);Assert.Equal(watermark,row.Billing.Watermark);Assert.Equal(30000,row.AgreementGrossCents);Assert.Equal(7500,row.Billing.Dues.Sum(d=>d.Cents));
        Assert.Empty(Read(f).Bookings);
    }

    [Fact]
    public void WindowAndStoreBoundariesAreValidatedBeforeReturningPlanningData()
    {
        using var f=new LedgerFixture();using var foreign=new BusinessFixture();var sources=new DashboardUpcomingSources(f.Business.Store);
        Assert.Throws<ArgumentException>(()=>sources.Load(f.Business.Owner,new(2027,1,31),new(2027,1,1),new(2027,3,31)));
        Assert.Throws<ArgumentException>(()=>sources.Load(f.Business.Owner,new(2027,1,1),new(2027,3,1),new(2027,2,28)));
        Assert.Throws<ArgumentException>(()=>sources.Load(f.Business.Owner,new(2027,1,1),new(2027,1,1),new(2037,1,1)));
        Assert.Throws<UnauthorizedAccessException>(()=>sources.Load(foreign.Owner,new(2027,1,1),new(2027,1,1),new(2027,3,31)));
        var last=sources.Load(f.Business.Owner,new(9999,12,1),new(9999,12,1),DateOnly.MaxValue);Assert.Equal(new DateOnly(9999,12,1),Assert.Single(last.Months).Month);
    }
}
