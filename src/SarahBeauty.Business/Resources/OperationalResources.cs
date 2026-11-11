using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Resources;

public sealed class OperationalResources(BusinessStore store)
{
    private readonly RecordStore<Supply> _supplies=new(store,"Supply");
    private readonly RecordStore<MileageTrip> _trips=new(store,"MileageTrip");
    public List<Supply> Supplies(OwnerSession owner)=>_supplies.List(owner);
    public List<MileageTrip> Trips(OwnerSession owner)=>_trips.List(owner);
    public List<Supply> Alerts(OwnerSession owner,DateOnly date)=>Supplies(owner).Where(s=>!s.Archived
        && (s.SupplyStatus is "Low" or "Finished" || s.ExpiryDate<=date)).ToList();

    public Supply SaveSupply(OwnerSession owner,Supply value)
    {
        if(string.IsNullOrWhiteSpace(value.Name) || !new[]{"Available","Low","Finished"}.Contains(value.SupplyStatus)
            || value.ExpiryDate<value.OpenedDate)throw new ArgumentException("Enter a supply name, supported status, and an expiry on or after opening.");
        value=value with{Id=value.Id.Length==0?Guid.NewGuid().ToString():value.Id,Name=value.Name.Trim()};
        return store.Write(owner,"Save supply",value.Id,db=>
        {
            var previous=value.Revision==0?null:_supplies.Get(db,owner,value.Id);
            if(!value.History.SequenceEqual(previous?.History??[]))throw new ArgumentException("Supply history cannot be overwritten.");
            if(previous is null || previous.SupplyStatus!=value.SupplyStatus || previous.OpenedDate!=value.OpenedDate || previous.ExpiryDate!=value.ExpiryDate)
                value=value with{History=[..value.History,new(value.SupplyStatus,value.OpenedDate,value.ExpiryDate,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"))]};
            return _supplies.Save(db,owner,value);
        });
    }

    public MileageTrip SaveTrip(OwnerSession owner,MileageTrip value)
    {
        if(string.IsNullOrWhiteSpace(value.Name) || value.TripDate is null || string.IsNullOrWhiteSpace(value.Origin)
            || string.IsNullOrWhiteSpace(value.Destination) || value.ActualMiles<=0 || value.ActualMiles>100000 || value.ParkingCents<0 || value.TollsCents<0
            || value.RequestedReimbursementCents<0 || value.ReimbursementStatus=="Requested" && value.RequestedReimbursementCents==0
            || value.ReimbursementStatus=="Not requested" && value.RequestedReimbursementCents>0
            || !new[]{"Unreviewed","Not requested","Requested"}.Contains(value.ReimbursementStatus))
            throw new ArgumentException("Enter the actual trip date, purpose, route, distance, costs, and reimbursement review status.");
        if((value.DriverOwnerId.Length>0)==(value.DriverArtistId.Length>0))throw new ArgumentException("Identify exactly one driver: an owner or an artist.");
        value=value with{Id=value.Id.Length==0?Guid.NewGuid().ToString():value.Id,Name=value.Name.Trim(),Origin=value.Origin.Trim(),Destination=value.Destination.Trim()};
        return store.Write(owner,"Save actual trip",value.Id,db=>
        {
            var previous=value.Revision==0?null:_trips.Get(db,owner,value.Id);
            if(!value.History.SequenceEqual(previous?.History??[]))throw new ArgumentException("Trip review history cannot be overwritten.");
            if(value.DriverOwnerId.Length>0 && db.Scalar("SELECT Id FROM Owners WHERE Id=$id AND BusinessId=$business AND (Active=1 OR $retained=1);",
                ("$id",value.DriverOwnerId),("$business",owner.BusinessId),("$retained",previous?.DriverOwnerId==value.DriverOwnerId?1:0)) is null)throw new ArgumentException("Choose an owner driver from this business.");
            if(value.DriverArtistId.Length>0 && !new RecordStore<Artist>(store,"Artist").List(db,owner).Any(a=>a.Id==value.DriverArtistId && (!a.Archived || previous?.DriverArtistId==a.Id)))
                throw new ArgumentException("Choose an active artist driver from this business.");
            if(value.BookingId.Length>0)_=new RecordStore<Booking>(store,"Booking").Get(db,owner,value.BookingId);
            value=value with{History=[..value.History,Review(value,owner)]};
            return _trips.Save(db,owner,value);
        });
    }

    private static TripReview Review(MileageTrip value,OwnerSession owner)=>new(checked(value.Revision+1),value.Name,value.TripDate!.Value,value.DriverOwnerId,value.DriverArtistId,
        value.BookingId,value.Origin,value.Destination,value.ActualMiles,value.ParkingCents,value.TollsCents,value.ReimbursementStatus,value.RequestedReimbursementCents,
        value.EvidenceId,value.Archived,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));
}
