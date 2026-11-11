using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Bookings;

public sealed class BookingBillingSources(BusinessStore store)
{
    internal Action? AfterBookingRead {get;set;}
    public BookingBillingSnapshot Load(OwnerSession owner,string bookingId,DateOnly asOf)=>store.ReadSnapshot(db=>Load(db,owner,bookingId,asOf));

    internal BookingBillingSnapshot Load(DbWork db,OwnerSession owner,string bookingId,DateOnly asOf,List<DocumentVersion>? versions=null)
    {
        store.RequireSession(db,owner);var booking=new RecordStore<Booking>(store,"Booking").Get(db,owner,bookingId);AfterBookingRead?.Invoke();
        versions??=DocumentVersions.List(db,owner);
        var invoices=versions.Where(v=>v.BookingId==booking.Id && v.Snapshot.Kind==DocumentKind.Invoice && v.Snapshot.IssueDate<=asOf && v.Status!="Void").ToArray();
        var credits=Credits(db,owner,booking.Id,asOf,versions).Where(c=>invoices.Any(i=>i.Id==c.InvoiceVersionId)).ToArray();
        var headers=Headers(db,owner,booking.Id);
        var dues=invoices.SelectMany(i=>ValueAvailability.Invoice(db,owner,i.Id,asOf)).ToArray();
        var rows=invoices.Select(i=>Invoice(db,owner,i,headers[i.Id],credits.Where(c=>c.InvoiceVersionId==i.Id).ToArray(),dues,asOf)).ToArray();
        return new((string)db.Scalar("SELECT DisplayName FROM Business WHERE Id=$id;",("$id",owner.BusinessId))!,
            (string)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!,ReceiptSnapshot.Watermark(db,owner),DateTimeOffset.UtcNow.ToString("O"),
            asOf,booking.Id,booking.Name,booking.Revision,rows,credits,dues);
    }

    private static BookingBillingInvoice Invoice(DbWork db,OwnerSession owner,DocumentVersion invoice,(string Payer,string Quote) header,
        BookingBillingCredit[] credits,InvoiceDue[] dues,DateOnly asOf)
    {
        var applied=ValueAvailability.ApplicationSum(db,"InvoiceVersionId",invoice.Id,asOf);
        var open=dues.Where(d=>d.InvoiceVersionId==invoice.Id).ToArray();
        var source=invoice.Snapshot;
        var result=new BookingBillingInvoice(invoice.Id,source.Number,header.Quote,header.Payer,source.Client.Name,source.IssueDate,source.DueDate,
            source.NetCents,source.TaxCents,credits.Sum(c=>c.NetCents),credits.Sum(c=>c.TaxCents),applied.Cents,applied.TaxCents,
            open.Sum(d=>d.Cents),open.Sum(d=>d.TaxCents),invoice.Hash,source.Lines);
        if(checked(result.EffectiveNetCents+result.EffectiveTaxCents)!=checked(result.AppliedCents+result.OpenCents)
            || result.EffectiveTaxCents!=checked(result.AppliedTaxCents+result.OpenTaxCents))
            throw new InvalidOperationException("The booking's issued charges, reductions, applications, and remaining balances do not agree. Review their original sources.");
        return result;
    }

    private static Dictionary<string,(string Payer,string Quote)> Headers(DbWork db,OwnerSession owner,string booking)=>db.Read("""
        SELECT H.VersionId,H.CustomerId,H.SourceQuoteVersionId FROM InvoiceHeaders H JOIN DocumentVersions V ON V.Id=H.VersionId
        JOIN Documents D ON D.Id=V.DocumentId WHERE D.BusinessId=$business AND D.BookingId=$booking;
        """,r=>(Id:r.GetString(0),Payer:r.GetString(1),Quote:r.GetString(2)),("$business",owner.BusinessId),("$booking",booking))
        .ToDictionary(h=>h.Id,h=>(h.Payer,h.Quote));

    private static BookingBillingCredit[] Credits(DbWork db,OwnerSession owner,string booking,DateOnly asOf,List<DocumentVersion> versions)
    {
        var sources=versions.ToDictionary(v=>v.Id);
        return db.Read("""
            SELECT H.VersionId,H.InvoiceVersionId,H.Date FROM CreditNoteHeaders H JOIN DocumentVersions V ON V.Id=H.VersionId
            JOIN Documents D ON D.Id=V.DocumentId WHERE D.BusinessId=$business AND D.BookingId=$booking AND H.Date<=$date ORDER BY H.Date,V.rowid;
            """,r=>(Id:r.GetString(0),Invoice:r.GetString(1),Date:DateOnly.Parse(r.GetString(2))),
            ("$business",owner.BusinessId),("$booking",booking),("$date",asOf.ToString("yyyy-MM-dd")))
            .Select(h=>new BookingBillingCredit(h.Id,h.Invoice,sources[h.Id].Snapshot.Number,h.Date,sources[h.Id].Snapshot.NetCents,
                sources[h.Id].Snapshot.TaxCents,sources[h.Id].Hash,sources[h.Id].Snapshot.Lines)).ToArray();
    }
}
