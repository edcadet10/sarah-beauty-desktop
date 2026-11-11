using Microsoft.UI.Xaml;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class BookingFinancialView
{
    internal static UIElement Build(BookingFinancialSnapshot source)
    {
        var panel=Form.Panel();
        panel.Add(Form.Title("Captured booking financial sources"),Form.Text($"Period {source.From:yyyy-MM-dd} through {source.Through:yyyy-MM-dd} • Watermark {source.Watermark}\n"
            +"Only explicitly linked source costs and receipts are attributed here. Signed receipt/refund amounts include dated posting corrections. A reversal is a book correction, not another physical customer payment."),
            Entries("Booking supporting financial entries",source.Entries,source.From),
            PagedReport.Build("Booking receipt and refund sources",["Date / scope","Movement / description","Gross net","Included tax net","Fees net","Net cash"],source.Cash.Select(c=>new[]{
                $"{c.Date:yyyy-MM-dd} • {(c.Date<source.From?"Before period":"In period")}",$"{c.Kind}{(c.Kind=="Reversal"?" of "+c.EffectiveKind:"")} • {c.Memo} • {c.MovementId}",
                Money.Display(c.SignedGrossCents),Money.Display(c.SignedTaxCents),Money.Display(c.SignedFeeCents),Money.Display(c.SignedNetCashCents)})));
        return panel;
    }

    internal static UIElement Entries(string title,IEnumerable<BookingFinancialEntry> entries,DateOnly from)=>PagedReport.Build(title,
        ["Date / scope","Movement / description","Account","Debit","Credit","Recorded direct cost","Service income"],entries.Select(e=>new[]{
            $"{e.Date:yyyy-MM-dd} • {(e.Date<from?"Before period":"In period")}",$"{e.Kind}{(e.Kind=="Reversal"?" of "+e.EffectiveKind:"")} • {e.Memo} • {e.MovementId}",e.AccountCode+" • "+e.AccountName,
            Money.Display(e.DebitCents),Money.Display(e.CreditCents),e.IsDirectCost?"Yes":"No",e.IsServiceIncome?"Yes":"No"}));
}
