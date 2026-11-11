using Microsoft.UI.Xaml;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class BookingBillingView
{
    internal static UIElement Build(BookingBillingSnapshot source)
    {
        var panel=Form.Panel();
        panel.Add(Form.Title("Captured booking billing sources"),
            Form.Text($"Billing as of {source.AsOf:yyyy-MM-dd} • Watermark {source.Watermark}. Invoice applications can include shared receipts or funded credits; they are not additional cash receipts."),
            PagedReport.Build("Booking invoice payers and balances",["Invoice / issued","Original payer","Effective net","Effective tax","Applied incl. tax","Open incl. tax"],
                source.Invoices.Select(i=>new[]{i.Number+" • "+i.IssueDate.ToString("yyyy-MM-dd"),i.PayerName,Money.Display(i.EffectiveNetCents),Money.Display(i.EffectiveTaxCents),Money.Display(i.AppliedCents),Money.Display(i.OpenCents)})),
            PagedReport.Build("Booking invoice source identities",["Invoice","Version identity","Source quote version","Snapshot hash"],source.Invoices.Select(i=>new[]{i.Number,i.VersionId,i.SourceQuoteVersionId,i.SourceHash})),
            PagedReport.Build("Booking issued invoice charge lines",["Invoice / charge","Quantity / unit","Net","Tax"],
                source.Invoices.SelectMany(i=>i.Lines.Select(l=>new[]{i.Number+" • "+l.Description,l.Quantity.ToString("G29")+" • "+l.Unit,Money.Display(l.NetCents),Money.Display(l.TaxCents)}))),
            PagedReport.Build("Booking credit-note reductions",["Note / date","Linked invoice","Net reduction","Tax reduction","Snapshot hash"],source.Credits.Select(c=>new[]{c.Number+" • "+c.Date.ToString("yyyy-MM-dd"),
                source.Invoices.Single(i=>i.VersionId==c.InvoiceVersionId).Number,Money.Display(c.NetCents),Money.Display(c.TaxCents),c.SourceHash})),
            PagedReport.Build("Booking credited charge lines",["Note / charge","Quantity / unit","Net reduction","Tax reduction"],
                source.Credits.SelectMany(c=>c.Lines.Select(l=>new[]{c.Number+" • "+l.Description,l.Quantity.ToString("G29")+" • "+l.Unit,Money.Display(l.NetCents),Money.Display(l.TaxCents)}))),
            PagedReport.Build("Booking remaining invoice milestones",["Invoice / milestone","Payer","Due date","Open incl. tax","Open tax"],source.Dues.Select(d=>new[]{d.Number+" • "+d.Kind,d.CustomerName,d.DueDate?.ToString("yyyy-MM-dd")??"Unknown",Money.Display(d.Cents),Money.Display(d.TaxCents)})));
        return panel;
    }
}
