using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

internal static class ReceiptSnapshot
{
    internal static ClientSnapshot Build(DbWork db,BusinessStore store,OwnerSession owner,ReceiptDraft draft,string number)
    {
        var payment=CorrectionRules.Original(db,owner,draft.PaymentId);
        if(payment.Kind is not (FinancialKind.Receipt or FinancialKind.AssetDisposal or FinancialKind.ReceiptClassification) || payment.AmountCents<=0 || draft.IssueDate is null || draft.IssueDate<payment.Date)
            throw new ArgumentException("Choose an approved customer payment and a receipt date on or after payment.");
        if(db.Scalar("SELECT Id FROM Movements WHERE ReversesId=$id AND Date<=$date;",("$id",draft.PaymentId),("$date",draft.IssueDate.Value.ToString("yyyy-MM-dd"))) is not null)
            throw new ArgumentException("This payment was reversed. Use its correction history instead of issuing a receipt.");
        if(draft.ClientReference.Length>200)throw new ArgumentException("Use a short client-safe payment reference of up to 200 characters.");
        if(draft.ReviewedWatermark!=Watermark(db,owner))throw new ArgumentException("Business records changed. Save and review the receipt again.");
        var customer=CustomerBook.Find(db,owner,payment.CustomerId)??throw new ArgumentException("The posted payment needs an identified payer.");
        var available=payment.Kind!=FinancialKind.AssetDisposal?ValueAvailability.Payment(db,owner,draft.PaymentId,draft.IssueDate.Value):new ValueBalance(0,0);
        var credits=CreditValues.List(db,owner).Where(c=>c.PaymentId==draft.PaymentId).ToList();
        long creditAvailable=credits.Sum(c=>CreditValues.Available(db,owner,c.Id,draft.IssueDate.Value).Cents);
        var invoices=DocumentVersions.List(db,owner).Where(v=>v.Snapshot.Kind==DocumentKind.Invoice && InvoiceVisibility.At(db,owner.BusinessId,v,draft.IssueDate.Value));
        long due=invoices.SelectMany(v=>ValueAvailability.InvoiceAsOf(db,owner.BusinessId,v.Id,draft.IssueDate.Value)).Where(d=>d.CustomerId==payment.CustomerId).Sum(d=>d.Cents);
        var received=payment.Kind==FinancialKind.ReceiptClassification?CorrectionRules.Original(db,owner,payment.OriginalId):payment;
        var detail=new ClientReceipt(received.Date!.Value,payment.PaymentMethod.Length==0?"Not recorded":payment.PaymentMethod,
            draft.ClientReference.Trim(),Applications(db,draft),Refunds(db,draft),available.Cents,creditAvailable,due);
        var booking=payment.BookingId.Length==0?null:new RecordStore<Booking>(store,"Booking").Get(db,owner,payment.BookingId);
        var clientEvent=payment.Kind==FinancialKind.AssetDisposal
            ?new ClientEvent("Asset sale: "+new RecordStore<Asset>(store,"Asset").Get(db,owner,payment.AssetId).Name,payment.Date,"","")
            :new ClientEvent(booking?.Name??"Payment acknowledgment",booking?.EventDate,booking?.Venue??"",booking?.ReadyBy??"");
        string business=(string)db.Scalar("SELECT DisplayName FROM Business WHERE Id=$id;",("$id",owner.BusinessId))!;
        string terms="Balances are shown as of this receipt's issue date. Later payments, refunds, and adjustments appear in their own history.";
        if(payment.Kind==FinancialKind.ReceiptClassification)terms=$"This receipt acknowledges a reviewed {Rules.Money.Display(payment.AmountCents)} portion of the payment received on {received.Date:yyyy-MM-dd}. It does not acknowledge an additional payment. "+terms;
        return new(DocumentKind.Receipt,number,1,"TEST",business,QuoteSnapshot.Party(customer),
            clientEvent,
            draft.IssueDate.Value,null,terms,
            [],checked(payment.AmountCents-payment.TaxCents),payment.TaxCents,payment.AmountCents,[]){Receipt=detail};
    }

    internal static long Watermark(DbWork db,OwnerSession owner)=>Convert.ToInt64(db.Scalar("SELECT Watermark FROM Business WHERE Id=$id;",("$id",owner.BusinessId)));

    private static ClientReceiptApplication[] Applications(DbWork db,ReceiptDraft draft)=>db.Read("""
        SELECT D.Number,M.Kind,SUM(CASE WHEN A.ReversesId IS NULL THEN A.Cents ELSE -A.Cents END)
        FROM PaymentApplications A JOIN InvoiceMilestones M ON M.Id=A.MilestoneId
        JOIN DocumentVersions V ON V.Id=A.InvoiceVersionId JOIN Documents D ON D.Id=V.DocumentId
        WHERE A.Date<=$date AND (A.PaymentId=$payment OR A.CreditId IN(SELECT Id FROM CustomerCredits WHERE PaymentId=$payment))
        GROUP BY D.Id,M.Kind HAVING SUM(CASE WHEN A.ReversesId IS NULL THEN A.Cents ELSE -A.Cents END)>0 ORDER BY D.Number,M.Kind;
        """,r=>new ClientReceiptApplication(r.GetString(0),r.GetString(1),r.GetInt64(2)),
        ("$payment",draft.PaymentId),("$date",draft.IssueDate!.Value.ToString("yyyy-MM-dd"))).ToArray();

    private static long Refunds(DbWork db,ReceiptDraft draft)=>Convert.ToInt64(db.Scalar("""
        SELECT COALESCE(SUM(M.GrossCents),0) FROM Movements M WHERE M.OriginalId=$payment AND M.Kind='Refund' AND M.Date<=$date
        AND NOT EXISTS(SELECT 1 FROM Movements R WHERE R.ReversesId=M.Id AND R.Date<=$date);
        """,("$payment",draft.PaymentId),("$date",draft.IssueDate!.Value.ToString("yyyy-MM-dd"))));
}
