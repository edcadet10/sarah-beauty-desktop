using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

internal static class InvoiceSnapshot
{
    internal static (DocumentVersion Quote,ClientSnapshot Snapshot) Create(DbWork db,BusinessStore store,OwnerSession owner,
        InvoiceDraft draft,string number)
    {
        var quote=DocumentVersions.Find(db,owner,id:draft.QuoteVersionId)??throw new ArgumentException("Choose an accepted quote.");
        var booking=new RecordStore<Booking>(store,"Booking").Get(db,owner,quote.BookingId);
        if(quote.Status!="Accepted" || quote.Snapshot.Kind!=DocumentKind.Quote || booking.AcceptedQuoteVersionId!=quote.Id)
            throw new ArgumentException("Choose the booking's current accepted quote version.");
        if(draft.CoverageGeneration!=InvoiceCoverage.Generation(db,booking.Id))throw new ArgumentException("Another invoice changed the unbilled amounts. Refresh and save this draft before issuing.");
        if(draft.IssueDate is null || draft.DueDate<draft.IssueDate)throw new ArgumentException("Choose valid invoice issue and due dates.");
        PostingValidation.CheckClosedPeriod(db,owner,draft.IssueDate.Value);
        InvoiceCoverage.ValidateVoidDates(db,owner,booking.Id,draft.IssueDate.Value);
        ValidatePayers(booking,draft);
        var payer=CustomerBook.Find(db,owner,draft.PayerCustomerId)??throw new ArgumentException("Choose the invoice payer.");
        if(draft.Portions.Length==0 || draft.Portions.Select(p=>p.ChargeId).Distinct().Count()!=draft.Portions.Length)
            throw new ArgumentException("Choose distinct source charges and their quantities.");
        var lines=draft.Portions.Select(p=>InvoiceCoverage.Portion(db,booking.Id,
            quote.Snapshot.Lines.SingleOrDefault(l=>l.ChargeId==p.ChargeId)??throw new ArgumentException("A charge does not belong to this quote."),p.Quantity)).ToArray();
        long total=lines.Sum(l=>l.TotalCents);
        var snapshot=quote.Snapshot with{Kind=DocumentKind.Invoice,Number=number,Version=1,Client=QuoteSnapshot.Party(payer),
            IssueDate=draft.IssueDate.Value,DueDate=draft.DueDate,Terms=draft.Terms,Lines=lines,
            NetCents=lines.Sum(l=>l.NetCents),TaxCents=lines.Sum(l=>l.TaxCents),TotalCents=total,
            Milestones=InvoiceCoverage.Milestones(db,owner,quote,total)};
        return(quote,snapshot);
    }

    private static void ValidatePayers(Booking booking,InvoiceDraft draft)
    {
        if(booking.Status=="Cancelled" || booking.PayerMode=="Undecided" || string.IsNullOrWhiteSpace(booking.UnpaidResponsibility))
            throw new ArgumentException("Resolve the payer arrangement and responsibility for unpaid shares before invoicing.");
        if(booking.PayerMode=="Single" && (booking.SinglePayerCustomerId.Length==0 || booking.SinglePayerCustomerId!=draft.PayerCustomerId))
            throw new ArgumentException("Use the explicitly selected single payer for this booking.");
        foreach(var portion in draft.Portions)
        {
            var charge=booking.Charges.SingleOrDefault(c=>c.Id==portion.ChargeId)??throw new ArgumentException("A booking charge is missing.");
            string assigned=booking.PayerMode=="Single"?booking.SinglePayerCustomerId:charge.PayerCustomerId;
            if((charge.Unit=="Mile" && assigned.Length==0) || (assigned.Length>0 && assigned!=draft.PayerCustomerId))
                throw new ArgumentException("This invoice's payer does not match the explicit charge/travel payer decision.");
        }
    }
}
