using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;

namespace SarahBeauty.Business.Finance;

public sealed record BookingReportCharge(string Id,string Description,string Unit,decimal Quantity,long UnitCents,long DiscountCents,long NetCents,
    decimal? TaxRate,bool Active,int? DurationMinutes);
public sealed record BookingReportPlace(string Id,string ChargeId,int Ordinal,string CustomerId,string CurrentName,bool Active);
public sealed record BookingReportAppointment(string Id,string Kind,string ArtistId,string CurrentArtistName,DateTimeOffset? Start,DateTimeOffset? End,
    string TimeZoneId,string Location,string Status,string[] PlaceIds);
public sealed record BookingReportFacts(string BookingId,string Name,int Revision,string Status,bool Archived,string CurrentPrimaryCustomerName,
    ClientEvent Event,bool HasTrial,BookingReportCharge[] Charges,BookingReportPlace[] Places,BookingReportAppointment[] Appointments);

public sealed record BookingReportPosition(BookingReportFacts CurrentFacts,BookingBillingSnapshot Billing,BookingFinancialSnapshot Financial)
{
    public long EffectiveNetChargesCents=>Billing.Invoices.Sum(i=>i.EffectiveNetCents);
    public long PeriodNetChargesCents=>checked(Billing.Invoices.Where(i=>i.IssueDate>=Financial.From).Sum(i=>i.OriginalNetCents)-Billing.Credits.Where(c=>c.Date>=Financial.From).Sum(c=>c.NetCents));
    public long? ContributionEstimateCents=>Billing.Invoices.Length==0?null:checked(EffectiveNetChargesCents-Financial.RecordedDirectCostCents);
    public long AppliedCents=>Billing.Invoices.Sum(i=>i.AppliedCents);
    public long OpenCents=>Billing.Invoices.Sum(i=>i.OpenCents);
}

public sealed record BookingReportSnapshot(int Version,List<BookingReportPosition> Positions,BookingFinancialEntry[] UnassignedDirectCosts);
