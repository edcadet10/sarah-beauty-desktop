using SarahBeauty.Business.Resources;

namespace SarahBeauty.Business.Exports;

internal static class ReadableOperationalTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;
        var supplies=new ReadableTableBuilder(source,budget,"Supplies","SupplyId","BusinessId","Revision","Name","Archived","Brand","Shade","OpenedDate","ExpiryDate","SupplyStatus","Location","PurchaseReference","CreatedAtUtc","CreatedByOwnerId","UpdatedAtUtc","UpdatedByOwnerId");
        var supplyHistory=new ReadableTableBuilder(source,budget,"Supply status history","SupplyId","BusinessId","Sequence","Status","OpenedDate","ExpiryDate","RecordedByOwnerId","AtUtc");ReadableRecords.ForEach<Supply>(db,source,"Supply",(value,audit)=>
        {
            supplies.Add(value.Id,source.BusinessId,value.Revision,value.Name,value.Archived,value.Brand,value.Shade,value.OpenedDate,value.ExpiryDate,value.SupplyStatus,value.Location,value.PurchaseReference,audit.CreatedAtUtc,audit.CreatedByOwnerId,audit.UpdatedAtUtc,audit.UpdatedByOwnerId);
            for(int i=0;i<value.History.Length;i++){var change=value.History[i];supplyHistory.Add(value.Id,source.BusinessId,i+1,change.Status,change.OpenedDate,change.ExpiryDate,change.OwnerId,change.AtUtc);}
        });
        var trips=new ReadableTableBuilder(source,budget,"Actual mileage trips","TripId","BusinessId","Revision","Purpose","Archived","TripDate","DriverOwnerId","DriverArtistId","BookingId","Origin","Destination","ActualMiles","ParkingCents","TollsCents","ReimbursementStatus","RequestedReimbursementCents","CreatedAtUtc","CreatedByOwnerId","UpdatedAtUtc","UpdatedByOwnerId");
        var tripHistory=new ReadableTableBuilder(source,budget,"Trip review history","TripId","BusinessId","Revision","Purpose","TripDate","DriverOwnerId","DriverArtistId","BookingId","Origin","Destination","ActualMiles","ParkingCents","TollsCents","ReimbursementStatus","RequestedReimbursementCents","Archived","ReviewedByOwnerId","AtUtc");ReadableRecords.ForEach<MileageTrip>(db,source,"MileageTrip",(value,audit)=>
        {
            trips.Add(value.Id,source.BusinessId,value.Revision,value.Name,value.Archived,value.TripDate,value.DriverOwnerId,value.DriverArtistId,value.BookingId,value.Origin,value.Destination,value.ActualMiles,value.ParkingCents,value.TollsCents,value.ReimbursementStatus,value.RequestedReimbursementCents,audit.CreatedAtUtc,audit.CreatedByOwnerId,audit.UpdatedAtUtc,audit.UpdatedByOwnerId);
            foreach(var review in value.History.OrderBy(h=>h.Revision))tripHistory.Add(value.Id,source.BusinessId,review.Revision,review.Purpose,review.TripDate,review.DriverOwnerId,review.DriverArtistId,review.BookingId,review.Origin,review.Destination,review.ActualMiles,review.ParkingCents,review.TollsCents,review.ReimbursementStatus,review.RequestedReimbursementCents,review.Archived,review.OwnerId,review.AtUtc);
        });
        var links=ReadableSqlTables.Read(db,source,budget,"Trip payment attributions",["LinkId","BusinessId","TripId","PaymentMovementId","Date","Cents","RequestId","PayloadSha256","SnapshotSha256","ApprovedByOwnerId","ApprovedAtUtc","ClaimBasisSha256","ReviewedTripRevision"],"SELECT Id,BusinessId,TripId,PaymentId,Date,Cents,RequestId,PayloadHash,SnapshotHash,ApprovedBy,AtUtc,json_extract(SnapshotJson,'$.ClaimBasisHash'),json_extract(SnapshotJson,'$.Trip.Revision') FROM TripPaymentLinks WHERE BusinessId=$business ORDER BY Date,Id;");
        var releases=ReadableSqlTables.Read(db,source,budget,"Trip attribution releases",["ReleaseId","BusinessId","LinkId","Date","Cents","RequestId","PayloadSha256","SnapshotSha256","ApprovedByOwnerId","ApprovedAtUtc"],"SELECT Id,BusinessId,LinkId,Date,Cents,RequestId,PayloadHash,SnapshotHash,ApprovedBy,AtUtc FROM TripPaymentReleases WHERE BusinessId=$business ORDER BY Date,Id;");
        return new[]{supplies.Freeze(),supplyHistory.Freeze(),trips.Freeze(),tripHistory.Freeze(),links,releases};
    });
}
