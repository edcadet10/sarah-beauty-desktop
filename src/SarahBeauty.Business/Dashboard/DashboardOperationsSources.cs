using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Dashboard;

public sealed class DashboardOperationsSources(BusinessStore store)
{
    internal Action? AfterRegistersRead {get;set;}
    public DashboardOperationsSnapshot Load(OwnerSession owner,DateOnly asOf)=>store.ReadSnapshot(db=>
        Load(db,owner,asOf,new DashboardFinanceSources(store).Load(db,owner,asOf,asOf)));

    internal DashboardOperationsSnapshot Load(DbWork db,OwnerSession owner,DateOnly asOf,DashboardFinanceSnapshot finance)
    {
        store.RequireSession(db,owner);var bookings=new RecordStore<Booking>(store,"Booking").List(db,owner).ToArray();AfterRegistersRead?.Invoke();
        var documents=DocumentVersions.List(db,owner);var artists=new RecordStore<Artist>(store,"Artist").List(db,owner).ToDictionary(a=>a.Id);
        var reader=new BookingBillingSources(store);var billing=bookings.ToDictionary(b=>b.Id,b=>reader.Load(db,owner,b.Id,asOf,documents));
        var items=DashboardDraftWork.Read(db,owner).ToList();items.AddRange(DashboardSetupWork.Read(db,store,owner,asOf,finance));
        items.AddRange(DashboardBookingWork.Read(db,owner,asOf,bookings,documents,artists,billing));items.AddRange(DashboardResourceWork.Trips(db,store,owner,asOf));
        items.AddRange(DashboardUnclassifiedWork.Read(db,owner,asOf));
        return new((string)db.Scalar("SELECT DisplayName FROM Business WHERE Id=$id;",("$id",owner.BusinessId))!,
            (string)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!,ReceiptSnapshot.Watermark(db,owner),DateTimeOffset.UtcNow.ToString("O"),asOf,
            items.OrderBy(i=>i.Kind).ThenBy(i=>i.Name).ToArray(),DashboardResourceWork.Supplies(db,store,owner,asOf));
    }
}
