using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;

namespace SarahBeauty.Business.Dashboard;

internal static class DashboardAgreementValue
{
    internal static long? Read(DocumentVersion quote,BookingBillingSnapshot billing,List<string> issues)
    {
        var invoices=billing.Invoices.Where(i=>i.SourceQuoteVersionId==quote.Id).ToArray();var ids=invoices.Select(i=>i.VersionId).ToHashSet();
        bool credited=billing.Credits.Any(c=>ids.Contains(c.InvoiceVersionId));
        if(credited && (invoices.Sum(i=>i.EffectiveNetCents)!=quote.Snapshot.NetCents || invoices.Sum(i=>i.EffectiveTaxCents)!=quote.Snapshot.TaxCents))
        {
            issues.Add("Credit notes affect this agreement. Review its remaining commitment or issue and accept an amended quote; an unrevised total is not included in the known value.");
            return null;
        }
        return quote.Snapshot.TotalCents;
    }
}
