using System.Globalization;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

internal static class CreditNoteReductions
{
    internal static List<ClientLine> Lines(DbWork db,string invoiceId,string? chargeId=null,DateOnly? through=null)=>db.Read("""
        SELECT L.ChargeId,L.Quantity,L.NetCents,L.TaxCents,L.DiscountCents FROM CreditNoteLines L
        JOIN CreditNoteHeaders H ON H.VersionId=L.NoteVersionId WHERE H.InvoiceVersionId=$invoice AND ($charge IS NULL OR L.ChargeId=$charge) AND ($through IS NULL OR H.Date<=$through);
        """,r=>new ClientLine(r.GetString(0),"","",decimal.Parse(r.GetString(1),CultureInfo.InvariantCulture),0,r.GetInt64(4),
            r.GetInt64(2),r.GetInt64(3),checked(r.GetInt64(2)+r.GetInt64(3))), ("$invoice",invoiceId),("$charge",chargeId),("$through",through?.ToString("yyyy-MM-dd")));

    internal static ValueBalance Milestone(DbWork db,string milestoneId,DateOnly date)
    {
        var rows=db.Read("""
            SELECT M.Cents,M.TaxCents FROM CreditNoteMilestones M JOIN CreditNoteHeaders H ON H.VersionId=M.NoteVersionId
            WHERE M.MilestoneId=$id AND H.Date<=$date;
            """,r=>new ValueBalance(r.GetInt64(0),r.GetInt64(1)),("$id",milestoneId),("$date",date.ToString("yyyy-MM-dd")));
        return new(rows.Sum(r=>r.Cents),rows.Sum(r=>r.TaxCents));
    }

    internal static IEnumerable<ClientMilestone> EffectiveMilestones(DbWork db,DocumentVersion invoice)=>
        db.Read("SELECT Id,Kind,Cents,DueDate FROM InvoiceMilestones WHERE InvoiceVersionId=$id;",
            r=>(Id:r.GetString(0),Kind:r.GetString(1),Cents:r.GetInt64(2),Due:r.IsDBNull(3)?(DateOnly?)null:DateOnly.Parse(r.GetString(3))),
            ("$id",invoice.Id)).Select(m=>new ClientMilestone(m.Kind,checked(m.Cents-Milestone(db,m.Id,DateOnly.MaxValue).Cents),m.Due));
}
