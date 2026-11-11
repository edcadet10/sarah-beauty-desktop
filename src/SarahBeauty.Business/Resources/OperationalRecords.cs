using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Resources;

public sealed record Supply : BusinessRecord
{
    public string Brand {get;init;}="";
    public string Shade {get;init;}="";
    public DateOnly? OpenedDate {get;init;}
    public DateOnly? ExpiryDate {get;init;}
    public string SupplyStatus {get;init;}="Available";
    public string Location {get;init;}="";
    public string Notes {get;init;}="";
    public string PurchaseReference {get;init;}="";
    public SupplyChange[] History {get;init;}=[];
    public override string ToString()=>$"{Name} • {SupplyStatus} • {(ExpiryDate.HasValue?$"Expires {ExpiryDate:yyyy-MM-dd}":"Expiry unknown")}";
}
public sealed record SupplyChange(string Status,DateOnly? OpenedDate,DateOnly? ExpiryDate,string OwnerId,string AtUtc);

public sealed record MileageTrip : BusinessRecord
{
    public DateOnly? TripDate {get;init;}
    public string DriverOwnerId {get;init;}="";
    public string DriverArtistId {get;init;}="";
    public string BookingId {get;init;}="";
    public string Origin {get;init;}="";
    public string Destination {get;init;}="";
    public decimal ActualMiles {get;init;}
    public long ParkingCents {get;init;}
    public long TollsCents {get;init;}
    public string ReimbursementStatus {get;init;}="Unreviewed";
    public string EvidenceId {get;init;}="";
    public override string ToString()=>$"{TripDate:yyyy-MM-dd} • {Name} • {ActualMiles} actual miles • {ReimbursementStatus}";
}
