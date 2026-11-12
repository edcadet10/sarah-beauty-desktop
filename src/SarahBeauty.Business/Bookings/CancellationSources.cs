using System.Text.Json;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Bookings;

public sealed class CancellationSources(BusinessStore store)
{
    internal Action? AfterBookingRead {get;set;}
    public CancellationSnapshot Load(OwnerSession owner,string bookingId,DateOnly asOf)=>store.ReadSnapshot(db=>Load(db,owner,bookingId,asOf));

    internal CancellationSnapshot Load(DbWork db,OwnerSession owner,string bookingId,DateOnly asOf)
    {
        store.RequireSession(db,owner);var booking=new RecordStore<Booking>(store,"Booking").Get(db,owner,bookingId);AfterBookingRead?.Invoke();
        var documents=DocumentVersions.List(db,owner);var own=documents.Where(d=>d.BookingId==booking.Id).ToArray();
        var invoices=own.Where(d=>d.Snapshot.Kind==DocumentKind.Invoice).ToArray();var invoiceIds=invoices.Select(d=>d.Id).ToHashSet();
        var applications=ApplicationWriter.List(db,owner);var credits=CreditValues.List(db,owner);
        var payments=Payments(db,owner);var related=Related(booking.Id,invoiceIds,applications,credits,payments);
        var sources=payments.Where(p=>related.Contains(p.Id)).Select(p=>Payment(db,owner,p.Id,p.Source,asOf)).ToArray();
        var creditRows=credits.Where(c=>related.Contains(c.PaymentId)).Select(c=>new CancellationCredit(c,CreditValues.Available(db,owner,c.Id,asOf),CreditValues.Available(db,owner,c.Id,DateOnly.MaxValue))).ToArray();
        var creditIds=creditRows.Select(c=>c.Source.Id).ToHashSet();
        var appRows=applications.Where(a=>a.ReversesId is null && (invoiceIds.Contains(a.InvoiceVersionId) || related.Contains(a.PaymentId??"") || creditIds.Contains(a.CreditId??"")))
            .Select(a=>Application(a,applications,documents,asOf)).ToArray();
        var dues=invoices.Where(d=>d.Status!="Void").SelectMany(d=>ValueAvailability.Invoice(db,owner,d.Id,asOf)).ToArray();
        string? closed=db.Scalar("SELECT ClosedThrough FROM BookSettings WHERE BusinessId=$id;",("$id",owner.BusinessId)) as string;
        return new((string)db.Scalar("SELECT DisplayName FROM Business WHERE Id=$id;",("$id",owner.BusinessId))!,
            (string)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!,Convert.ToInt64(db.Scalar("SELECT Watermark FROM Business WHERE Id=$id;",("$id",owner.BusinessId))),
            DateTimeOffset.UtcNow.ToString("O"),asOf,booking.Id,booking.Revision,booking.Name,booking.Status,QuoteSnapshot.Event(booking),booking.AcceptedQuoteVersionId,
            closed is null?null:StoredDates.Parse(closed),own,sources,creditRows,appRows,dues,Charges(db,invoices,asOf));
    }

    private static List<(string Id,FinancialDraft Source)> Payments(DbWork db,OwnerSession owner)=>db.Read("""
        SELECT Id,DataJson FROM Movements WHERE BusinessId=$business AND Kind IN('Receipt','UnclassifiedReceipt','ReceiptClassification') ORDER BY Date,Id;
        """,r=>(r.GetString(0),JsonSerializer.Deserialize<FinancialDraft>(r.GetString(1))!),("$business",owner.BusinessId));

    private static HashSet<string> Related(string booking,HashSet<string> invoices,List<PaymentApplication> applications,List<CustomerCredit> credits,List<(string Id,FinancialDraft Source)> payments)
    {
        var related=payments.Where(p=>p.Source.BookingId==booking).Select(p=>p.Id).ToHashSet();
        foreach(var app in applications.Where(a=>invoices.Contains(a.InvoiceVersionId)))
            related.Add(app.PaymentId??credits.Single(c=>c.Id==app.CreditId).PaymentId);
        bool changed;
        do
        {
            changed=false;
            foreach(var p in payments.Where(p=>p.Source.Kind==FinancialKind.ReceiptClassification))
                if(related.Contains(p.Id) || related.Contains(p.Source.OriginalId)){changed|=related.Add(p.Id);changed|=related.Add(p.Source.OriginalId);}
        }while(changed);
        return related;
    }

    private static CancellationPayment Payment(DbWork db,OwnerSession owner,string id,FinancialDraft source,DateOnly date)
    {
        string name=db.Scalar("SELECT json_extract(DataJson,'$.FullName') FROM Customers WHERE Id=$id AND BusinessId=$business;",("$id",source.CustomerId),("$business",owner.BusinessId)) as string??"Payer undecided";
        string classification=db.Scalar("SELECT Name FROM Accounts WHERE Id=$id;",("$id",source.CounterAccountId)) as string??"Unclassified suspense";
        string? cancelled=db.Scalar("SELECT Date FROM Movements WHERE ReversesId=$id;",("$id",id)) as string;
        return new(id,source.Kind,source.BookingId,source.OriginalId,source.CustomerId,name,source.Date!.Value,source.AmountCents,source.TaxCents,
            ValueAvailability.Payment(db,owner,id,date),ValueAvailability.Payment(db,owner,id,DateOnly.MaxValue),classification,source.EvidenceId,cancelled is null?null:StoredDates.Parse(cancelled));
    }

    private static CancellationApplication Application(PaymentApplication source,List<PaymentApplication> all,List<DocumentVersion> documents,DateOnly date)
    {
        var invoice=documents.Single(d=>d.Id==source.InvoiceVersionId);var reversals=all.Where(a=>a.ReversesId==source.Id).ToArray();
        ValueBalance Remaining(DateOnly at)=>source.Date>at?new(0,0):new(source.Cents-reversals.Where(r=>r.Date<=at).Sum(r=>r.Cents),source.TaxCents-reversals.Where(r=>r.Date<=at).Sum(r=>r.TaxCents));
        return new(source.Id,source.PaymentId??"",source.CreditId??"",invoice.Id,invoice.Snapshot.Number,invoice.BookingId,source.MilestoneId,source.Date,source.Cents,Remaining(date),Remaining(DateOnly.MaxValue));
    }

    private static CancellationCharge[] Charges(DbWork db,DocumentVersion[] invoices,DateOnly date)=>invoices.SelectMany(invoice=>invoice.Snapshot.Lines.Select(line=>
        new CancellationCharge(invoice.Id,line,invoice.Snapshot.IssueDate>date?0:line.Quantity-CreditNoteReductions.Lines(db,invoice.Id,line.ChargeId,date).Sum(c=>c.Quantity),
            line.Quantity-CreditNoteReductions.Lines(db,invoice.Id,line.ChargeId).Sum(c=>c.Quantity)))).ToArray();
}
