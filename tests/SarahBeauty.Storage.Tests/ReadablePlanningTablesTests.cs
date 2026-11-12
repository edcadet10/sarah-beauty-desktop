using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Exports;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadablePlanningTablesTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];
    [Fact]
    public void RetainedCatalogPricesNamedAndUnnamedPlacesAndUnknownTaxRemainDistinct()
    {
        using var f=new LedgerFixture();var catalog=new ServiceCatalog(f.Business.Store);var book=new BookingBook(f.Business.Store);var booking=catalog.NewWedding(f.Business.Owner,"Fictional readable wedding",2,76.6m);const string secret="Fictional-private-planning-sentinel";booking=book.Save(f.Business.Owner,booking with{PrivateNotes=secret});string guestCharge=booking.Charges.Single(c=>c.Unit=="Person").Id;booking=book.LinkPlace(f.Business.Owner,booking,booking.Places.First(p=>p.ChargeId==guestCharge).Id,f.CustomerId);booking=book.Save(f.Business.Owner,BookingRules.ChangeQuantity(booking,guestCharge,1));var guest=catalog.List(f.Business.Owner).Single(s=>s.Code=="GUEST");catalog.Save(f.Business.Owner,guest with{UnitCents=13500,TaxReviewEvidence=secret});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadablePlanningTables.Build(source,new());Assert.Equal(5,tables.Length);Assert.DoesNotContain(secret,JsonSerializer.Serialize(tables));var services=tables.Single(t=>t.Name=="Service catalog");var current=services.Rows.Single(r=>Cell(services,r,"ServiceId")==guest.Id);Assert.Equal("13500",Cell(services,current,"UnitCents"));var history=tables.Single(t=>t.Name=="Service revisions");Assert.Equal(new[]{"12000","13500"},history.Rows.Where(r=>Cell(history,r,"ServiceId")==guest.Id).Select(r=>Cell(history,r,"UnitCents")));
        var charges=tables.Single(t=>t.Name=="Booking charges");Assert.Equal("12000",Cell(charges,charges.Rows.Single(r=>r[0]==guestCharge),"UnitCents"));var travel=charges.Rows.Single(r=>Cell(charges,r,"Unit")=="Mile");Assert.Equal("76.6",Cell(charges,travel,"Quantity"));Assert.Equal("7660",Cell(charges,travel,"NetCents"));var parents=tables.Single(t=>t.Name=="Bookings");var row=Assert.Single(parents.Rows);Assert.Equal("49660",Cell(parents,row,"NetCents"));Assert.Equal("",Cell(parents,row,"TaxCents"));Assert.Equal("",Cell(parents,row,"TotalCents"));Assert.Equal("",Cell(parents,row,"PlannedDepositCents"));Assert.Equal("2",Cell(parents,row,"ActivePlaces"));Assert.Equal("1",Cell(parents,row,"NamedActivePlaces"));var places=tables.Single(t=>t.Name=="Booking places");Assert.Equal(3,places.Rows.Count);Assert.Contains(places.Rows,r=>Cell(places,r,"Active")=="false");Assert.Contains(places.Rows,r=>Cell(places,r,"CustomerId")=="");
    }
    [Fact]
    public void ConfirmedZeroTaxAndPlanValuesUseExistingBookingRulesWithoutInventingDueDates()
    {
        using var f=new LedgerFixture();var catalog=new ServiceCatalog(f.Business.Store);var booking=catalog.NewWedding(f.Business.Owner,"Fictional zero-tax plan",2);booking=new BookingBook(f.Business.Store).Save(f.Business.Owner,booking with{Charges=booking.Charges.Select(c=>c with{TaxRate=0m}).ToArray()});using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadablePlanningTables.Build(source,new());var parent=tables.Single(t=>t.Name=="Bookings");var row=Assert.Single(parent.Rows);Assert.Equal("0",Cell(parent,row,"TaxCents"));Assert.Equal("54000",Cell(parent,row,"TotalCents"));Assert.Equal("15000",Cell(parent,row,"PlannedTrialCents"));Assert.Equal("19500",Cell(parent,row,"PlannedDepositCents"));Assert.Equal("19500",Cell(parent,row,"PlannedFinalCents"));Assert.Equal("",Cell(parent,row,"EventDate"));Assert.Equal("",Cell(parent,row,"DepositDue"));
    }
    [Fact]
    public void MismatchedRetainedRecordIdentityStopsProjectionWithoutPublishingAnotherRecord()
    {
        using var f=new LedgerFixture();var catalog=new ServiceCatalog(f.Business.Store);catalog.Initialize(f.Business.Owner);var service=catalog.List(f.Business.Owner).First();f.Business.Store.Transaction(db=>db.Execute("UPDATE Records SET DataJson=json_set(DataJson,'$.Id',$other) WHERE Id=$id;",("$other",Guid.NewGuid().ToString()),("$id",service.Id)));using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);Assert.Throws<InvalidDataException>(()=>ReadablePlanningTables.Build(source,new()));
    }
}
