using Microsoft.UI.Xaml;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal static class MoneyReviewDetails
{
    internal static UIElement Build(BusinessStore store,OwnerSession owner,FinancialDraft draft)
    {
        var panel=Form.Panel();
        panel.Add(Form.Text($"Draft reference: {draft.Id}\nSaved revision: {draft.Revision}"));
        if(draft.PostedBatchId.Length>0)panel.Add(Form.Text("Posted movement reference: "+draft.PostedBatchId));
        if(FinancialFields.ShowsPayer(draft.Kind)||draft.CustomerId.Length>0)
            panel.Add(Form.Text(Named("Current payer / customer",draft.CustomerId,()=>new CustomerBook(store).Search(owner,includeArchived:true).FirstOrDefault(c=>c.Id==draft.CustomerId)?.FullName)));
        if(draft.BookingId.Length>0)
            panel.Add(Form.Text(Named("Current related booking",draft.BookingId,()=>new BookingBook(store).List(owner).FirstOrDefault(b=>b.Id==draft.BookingId)?.Name)));
        if(FinancialFields.ShowsPaymentMethod(draft.Kind)||draft.PaymentMethod.Length>0)
            panel.Add(Form.Text("Payment method: "+Known(draft.PaymentMethod)));
        if(draft.Kind!=FinancialKind.ReceiptClassification||draft.ExternalReference.Length>0||draft.Provider.Length>0)
            panel.Add(Form.Text($"External reference: {Known(draft.ExternalReference)}\nReference provider: {Known(draft.Provider)}\nReference claimed unique: {(draft.ReliableReference?"Yes":"No")}"));
        if(FinancialFields.ShowsTaxReview(draft.Kind))
            panel.Add(Form.Text($"Tax review: {(draft.TaxResolved?"Resolved":"Unresolved")} • Included sales tax: {Money.Display(draft.TaxCents)}"));
        return panel;
    }

    private static string Named(string label,string id,Func<string?> name)=>id.Length==0?label+": Not selected":
        $"{label}: {name()??"Current name unavailable"}\nReference: {id}";
    private static string Known(string value)=>string.IsNullOrWhiteSpace(value)?"Not recorded":value;
}
