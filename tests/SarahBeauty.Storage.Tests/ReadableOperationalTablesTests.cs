using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableOperationalTablesTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];
    [Fact]
    public void SuppliesRetainKnownAndUnknownDatesAndStatusHistoryWithoutPrivateNotesOrPosting()
    {
        using var f=new LedgerFixture();var book=new OperationalResources(f.Business.Store);const string secret="Fictional-private-supply-note";var first=book.SaveSupply(f.Business.Owner,new(){Name="Fictional readable supply",Brand="Fictional brand",Shade="Fictional shade",Notes=secret});book.SaveSupply(f.Business.Owner,first with{SupplyStatus="Low",OpenedDate=new(2027,1,1),ExpiryDate=new(2027,2,1)});using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableOperationalTables.Build(source,new());Assert.Equal(6,tables.Length);Assert.DoesNotContain(secret,JsonSerializer.Serialize(tables));var supply=tables.Single(t=>t.Name=="Supplies");Assert.Equal("2027-02-01",Cell(supply,supply.Rows.Single(),"ExpiryDate"));var history=tables.Single(t=>t.Name=="Supply status history");Assert.Equal(2,history.Rows.Count);Assert.Equal("",Cell(history,history.Rows.First(),"ExpiryDate"));Assert.Equal("Low",Cell(history,history.Rows.Last(),"Status"));Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Movements;"));
    }
    [Fact]
    public void ActualMileageAndReviewedClaimsStaySeparateFromCustomerTravelChargesAndUnknownVersusZero()
    {
        using var f=new LedgerFixture();var booking=new BookingBook(f.Business.Store).Save(f.Business.Owner,BookingRules.Wedding("Fictional separately billed travel",2,76.6m));var book=new OperationalResources(f.Business.Store);const string secret="Fictional-private-trip-evidence";var first=book.SaveTrip(f.Business.Owner,TripReviewTests.Trip(f) with{BookingId=booking.Id,ReimbursementStatus="Requested",EvidenceId=secret});var second=book.SaveTrip(f.Business.Owner,first with{ActualMiles=24.8m,RequestedReimbursementCents=2150});book.SaveTrip(f.Business.Owner,second with{ReimbursementStatus="Not requested",RequestedReimbursementCents=0});using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableOperationalTables.Build(source,new());Assert.DoesNotContain(secret,JsonSerializer.Serialize(tables));var trips=tables.Single(t=>t.Name=="Actual mileage trips");Assert.Equal("24.8",Cell(trips,trips.Rows.Single(),"ActualMiles"));Assert.Equal("0",Cell(trips,trips.Rows.Single(),"RequestedReimbursementCents"));var history=tables.Single(t=>t.Name=="Trip review history");Assert.Equal(new[]{"","2150","0"},history.Rows.Select(r=>Cell(history,r,"RequestedReimbursementCents")));var charges=ReadablePlanningTables.Build(source,new()).Single(t=>t.Name=="Booking charges");Assert.Equal("76.6",Cell(charges,charges.Rows.Single(r=>Cell(charges,r,"Unit")=="Mile"),"Quantity"));Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Movements;"));
    }
    [Fact]
    public void ActualPaymentAttributionAndReleaseRetainTheReviewedBasisWithoutAnotherExpense()
    {
        using var f=new LedgerFixture();var trip=TripPaymentStorageTests.Trip(f);var payment=f.Post(f.Draft(FinancialKind.Expense,10000));var book=new TripPayments(f.Business.Store);var draft=TripPaymentStorageTests.Draft(f,trip,payment,6000) with{PayeeReview="Fictional-private-payee-review",EvidenceId="Fictional-private-payment-evidence"};var link=TripPaymentStorageTests.Link(f,book,draft);var release=TripPaymentStorageTests.Release(f,book,link,2000);using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableOperationalTables.Build(source,new());Assert.DoesNotContain("Fictional-private",JsonSerializer.Serialize(tables));var links=tables.Single(t=>t.Name=="Trip payment attributions");var row=Assert.Single(links.Rows);Assert.Equal(payment.Id,Cell(links,row,"PaymentMovementId"));Assert.Equal("6000",Cell(links,row,"Cents"));Assert.Equal(link.Snapshot.ClaimBasisHash,Cell(links,row,"ClaimBasisSha256"));Assert.Equal(trip.Revision.ToString(),Cell(links,row,"ReviewedTripRevision"));var releases=tables.Single(t=>t.Name=="Trip attribution releases");Assert.Equal(release.Id,releases.Rows.Single()[0]);Assert.Equal(link.Id,Cell(releases,releases.Rows.Single(),"LinkId"));Assert.Equal("2000",Cell(releases,releases.Rows.Single(),"Cents"));Assert.Equal(1L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Movements;"));
    }
}
