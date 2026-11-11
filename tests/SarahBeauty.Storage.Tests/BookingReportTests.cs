using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class BookingReportTests
{
    private static ReportSnapshot Report(LedgerFixture f,DateOnly? through=null)=>new FinancialReports(f.Business.Store).Load(f.Business.Owner,new(2027,1,1),through??new(2027,1,31));

    private static void Apply(LedgerFixture f,DocumentVersion invoice,string payment,long cents)
    {
        var apps=new PaymentApplications(f.Business.Store);var draft=apps.SaveDraft(f.Business.Owner,new PaymentApplicationDraft{
            Name="Fictional booking report application",InvoiceVersionId=invoice.Id,PaymentId=payment,Cents=cents,Date=f.Date});
        apps.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }

    [Fact]
    public void IssuedChargeEstimateIsSeparateFromCashIncomeApplicationsAndUnassignedCosts()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000);
        var tagged=f.Post(f.Draft(FinancialKind.Receipt,5000) with{BookingId=invoice.BookingId,FeeCents=100});
        var untagged=f.Post(f.Draft(FinancialKind.Receipt,2000));Apply(f,invoice,tagged.Id,3000);Apply(f,invoice,untagged.Id,2000);
        f.Post(f.Draft(FinancialKind.Expense,2000) with{BookingId=invoice.BookingId});f.Post(f.Draft(FinancialKind.Expense,900));
        var report=Report(f);var booking=Assert.Single(report.Bookings!.Positions);
        Assert.Equal(10000,booking.EffectiveNetChargesCents);Assert.Equal(7900,booking.ContributionEstimateCents);
        Assert.Equal(2100,booking.Financial.RecordedDirectCostCents);Assert.Equal(5000,booking.Financial.RecognizedServiceIncomeCents);
        Assert.Equal(5000,booking.Financial.Cash.Sum(c=>c.SignedGrossCents));Assert.Equal(4900,booking.Financial.Cash.Sum(c=>c.SignedNetCashCents));
        Assert.Equal(5000,booking.AppliedCents);Assert.Equal(5000,booking.OpenCents);Assert.Equal(900,Assert.Single(report.Bookings.UnassignedDirectCosts).DebitCents);
        Assert.Equal(7000,report.ProfitLoss.IncomeCents);Assert.Equal(3000,report.ProfitLoss.ExpenseCents);
        Assert.Equal(report.Watermark,booking.Billing.Watermark);Assert.Equal(report.Watermark,booking.Financial.Watermark);
    }

    [Fact]
    public void UnissuedChargesRemainWithoutAnEstimateAndOperationalFactsExcludePrivateFields()
    {
        using var f=new LedgerFixture();var book=new BookingBook(f.Business.Store);var artist=book.SaveArtist(f.Business.Owner,new(){Name="Fictional report artist",Notes="PRIVATE-ARTIST"});
        var booking=BookingRules.Wedding("Fictional report event",1) with{PrimaryCustomerId=f.CustomerId,PrivateNotes="PRIVATE-BOOKING"};
        booking=book.Save(f.Business.Owner,booking with{Places=booking.Places.Select((p,i)=>i==0?p with{CustomerId=f.CustomerId}:p).ToArray(),
            Appointments=[new(Guid.NewGuid().ToString(),"Wedding",artist.Id,DateTimeOffset.Parse("2027-01-10T10:00:00-05:00"),DateTimeOffset.Parse("2027-01-10T11:00:00-05:00"),"Fictional venue",PrivateNotes:"PRIVATE-APPOINTMENT",ProductsAndShades:"PRIVATE-PRODUCTS")
                {TimeZoneId="America/New_York",PlaceIds=[booking.Places[0].Id]}]});
        f.Post(f.Draft(FinancialKind.Expense,2000) with{BookingId=booking.Id});var report=Report(f);var position=Assert.Single(report.Bookings!.Positions);
        Assert.Null(position.ContributionEstimateCents);Assert.Empty(position.Billing.Invoices);Assert.Equal(2000,position.Financial.RecordedDirectCostCents);
        Assert.Equal(2,position.CurrentFacts.Places.Length);Assert.Equal("Fictional payer",position.CurrentFacts.Places.Single(p=>p.CustomerId.Length>0).CurrentName);
        Assert.Equal("Unnamed recipient",position.CurrentFacts.Places.Single(p=>p.CustomerId.Length==0).CurrentName);
        Assert.Equal("Fictional report artist",Assert.Single(position.CurrentFacts.Appointments).CurrentArtistName);Assert.Null(position.CurrentFacts.Event.Date);
        Assert.DoesNotContain("PRIVATE-",JsonSerializer.Serialize(report));Assert.True(position.CurrentFacts.HasTrial);
    }

    [Fact]
    public void CancelledAndArchivedBookingKeepsItsActualChargesAndCapturedEstimatesStayUnchanged()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000);var books=new BookingBook(f.Business.Store);var booking=books.List(f.Business.Owner).Single();
        booking=books.ChangeStatus(f.Business.Owner,booking,"Cancelled","Fictional status-only decision");books.Save(f.Business.Owner,booking with{Archived=true});
        f.Post(f.Draft(FinancialKind.Expense,2000) with{BookingId=booking.Id});string json=JsonSerializer.Serialize(Report(f));
        var captured=JsonSerializer.Deserialize<ReportSnapshot>(json)!;var prior=Assert.Single(captured.Bookings!.Positions);
        Assert.Equal(8000,prior.ContributionEstimateCents);Assert.Equal(10000,prior.OpenCents);Assert.True(prior.CurrentFacts.Archived);
        Assert.Equal("Cancelled",prior.CurrentFacts.Status);Assert.False(prior.CurrentFacts.HasTrial);
        f.Post(f.Draft(FinancialKind.Expense,1000) with{BookingId=booking.Id});Assert.Equal(7000,Assert.Single(Report(f).Bookings!.Positions).ContributionEstimateCents);
        Assert.Equal(8000,prior.ContributionEstimateCents);
        var legacy=JsonSerializer.SerializeToNode(captured)!.AsObject();legacy.Remove("Bookings");Assert.Null(JsonSerializer.Deserialize<ReportSnapshot>(legacy.ToJsonString())!.Bookings);
    }

    [Fact]
    public void ConcurrentCreditAndExpenseCannotMixTheCombinedReportOrItsWatermark()
    {
        using var f=new LedgerFixture();f.Business.Files.Execute("PRAGMA journal_mode=WAL;");var invoice=ApplicationTests.Invoice(f,10000);
        f.Post(f.Draft(FinancialKind.Expense,2000) with{BookingId=invoice.BookingId});
        long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        var reports=new FinancialReports(f.Business.Store){AfterTrialRead=()=>
        {
            var credits=new CreditNoteBook(f.Business.Store);var draft=credits.SaveDraft(f.Business.Owner,new CreditNoteDraft{
                Name="Fictional concurrent charge reduction",InvoiceVersionId=invoice.Id,IssueDate=f.Date,Portions=[new(invoice.Snapshot.Lines.Single().ChargeId,0.5m)]});
            credits.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);f.Post(f.Draft(FinancialKind.Expense,1000) with{BookingId=invoice.BookingId});
        }};
        var snapshot=reports.Load(f.Business.Owner,new(2027,1,1),new(2027,1,31));var prior=Assert.Single(snapshot.Bookings!.Positions);
        Assert.Equal(8000,prior.ContributionEstimateCents);Assert.Equal(watermark,snapshot.Watermark);Assert.Equal(watermark,prior.Billing.Watermark);Assert.Equal(watermark,prior.Financial.Watermark);
        Assert.Equal(2000,Assert.Single(Report(f).Bookings!.Positions).ContributionEstimateCents);
    }
}
