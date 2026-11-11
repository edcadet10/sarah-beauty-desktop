using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Bookings;

public sealed record Booking : BusinessRecord
{
    public string PrimaryCustomerId { get; init; } = "";
    public string EventType { get; init; } = "Wedding";
    public DateOnly? EventDate { get; init; }
    public string Venue { get; init; } = "";
    public string ReadyBy { get; init; } = "";
    public string EventTimeZoneId {get;init;}="America/New_York";
    public string ReadyByUtcOffset {get;init;}="";
    public string Status { get; init; } = "Inquiry";
    public string PayerMode { get; init; } = "Undecided";
    public string SinglePayerCustomerId { get; init; } = "";
    public string UnpaidResponsibility { get; init; } = "";
    public string AcceptedQuoteVersionId { get; init; } = "";
    public bool HasTrial { get; init; } = true;
    public long TrialCents { get; init; } = 15000;
    public int? TrialDurationMinutes {get;init;}
    public long? CustomDepositCents { get; init; }
    public DateOnly? TrialDue { get; init; }
    public DateOnly? DepositDue { get; init; }
    public DateOnly? FinalDue { get; init; }
    public string PrivateNotes { get; init; } = "";
    public BookingCharge[] Charges { get; init; } = [];
    public BookingPlace[] Places { get; init; } = [];
    public Appointment[] Appointments { get; init; } = [];
    public BookingStatusChange[] History { get; init; } = [];
    public BookingReservation[] Reservations { get; init; } = [];
    public override string ToString() => $"{Name} • {EventDate?.ToString("yyyy-MM-dd") ?? "Date undecided"} • {Status}";
}

public sealed record BookingCharge(string Id, string Description, string Unit, decimal Quantity,
    long UnitCents, long DiscountCents = 0, decimal? TaxRate = null, string PayerCustomerId = "", bool Active = true)
{
    public string ServiceId {get;init;}="";
    public int ServiceRevision {get;init;}
    public int? DurationMinutes {get;init;}
    public long NetCents => checked(Money.Multiply(Quantity, UnitCents) - DiscountCents);
    public bool PersonBased => Unit is "Person" or "Package";
    public override string ToString() => $"{Description} • {Quantity} × {Money.Display(UnitCents)} • {Money.Display(NetCents)}";
}

public sealed record BookingPlace(string Id, string ChargeId, int Ordinal, string CustomerId = "", bool Active = true);
public sealed record Artist : BusinessRecord { public string Notes { get; init; } = ""; }
public sealed record Appointment(string Id, string Kind, string ArtistId, DateTimeOffset? Start, DateTimeOffset? End,
    string Location, string Status = "Planned", string OverrideReason = "", string ProductsAndShades = "", string PrivateNotes = "")
{
    public string[] PlaceIds {get;init;}=[];
    public string TimeZoneId {get;init;}="";
}
public sealed record BookingStatusChange(string From, string To, string Reason, string OwnerId, string AtUtc);
public sealed record BookingReservation(string RequestId,string QuoteVersionId,DateOnly Date,long TrialCents,long DepositCents,
    string Reason,string OwnerId,string AtUtc);
public sealed record BookingTotals(long NetCents, long? TaxCents, long? TotalCents, PaymentPlan? Plan);
