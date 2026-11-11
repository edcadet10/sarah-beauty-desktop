using SarahBeauty.Business.Bookings;

namespace SarahBeauty.Business.Dashboard;

public sealed record DashboardUpcomingArtist(string Id,string Name,string State);
public sealed record DashboardUpcomingBooking(string BookingId,string Name,DateOnly? EventDate,string Venue,string ReadyBy,string TimeZoneId,
    DashboardUpcomingArtist[] Artists,string QuoteVersionId,string QuoteLabel,string QuoteHash,long? AgreementGrossCents,string[] ReviewIssues,BookingBillingSnapshot Billing);
public sealed record DashboardUpcomingMonth(DateOnly Month,int Count,long KnownAgreementGrossCents,int UnknownValueCount,string[] BookingIds);
public sealed record DashboardUpcomingSnapshot(string BusinessName,string Mode,long Watermark,string GeneratedAtUtc,DateOnly PlanningAsOf,DateOnly From,DateOnly Through,
    DashboardUpcomingBooking[] Bookings,DashboardUpcomingBooking[] Undated,DashboardUpcomingMonth[] Months);
