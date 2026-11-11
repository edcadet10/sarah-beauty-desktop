using System.Globalization;
using System.Text.Json;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

internal static class InvoiceCoverage
{
    internal static long Generation(DbWork db,string bookingId)=>Convert.ToInt64(db.Scalar(
        "SELECT Generation FROM BillingState WHERE BookingId=$id;",("$id",bookingId))??0L);

    internal static List<CoveredPortion> Existing(DbWork db,string bookingId,string chargeId)=>db.Read("""
        SELECT C.Quantity,C.NetCents,C.TaxCents,C.DiscountCents,C.SourceSignature FROM InvoiceCoverage C
        JOIN DocumentVersions V ON V.Id=C.InvoiceVersionId
        WHERE C.BookingId=$booking AND C.ChargeId=$charge AND V.Status<>'Void';
        """,r=>new CoveredPortion(decimal.Parse(r.GetString(0),CultureInfo.InvariantCulture),r.GetInt64(1),r.GetInt64(2),r.GetInt64(3),r.GetString(4)),
        ("$booking",bookingId),("$charge",chargeId));

    internal static ClientLine Portion(DbWork db,string bookingId,ClientLine source,decimal quantity)
    {
        var used=Existing(db,bookingId,source.ChargeId);
        decimal available=source.Quantity-used.Sum(u=>u.Quantity);
        if(quantity<=0 || quantity>available)throw new ArgumentException($"The quantity for {source.Description} exceeds its unbilled amount.");
        if(used.Any(u=>u.Signature!=Signature(source)))throw new ArgumentException("Credit previously billed charges before changing their price or quantity. Add a new charge for additional services.");
        bool final=quantity==available;
        long net=Share(source.NetCents,used.Sum(u=>u.NetCents),quantity,source.Quantity,final);
        long tax=Share(source.TaxCents,used.Sum(u=>u.TaxCents),quantity,source.Quantity,final);
        long discount=Share(source.DiscountCents,used.Sum(u=>u.DiscountCents),quantity,source.Quantity,final);
        long rounding=checked(net-Money.Multiply(quantity,source.UnitCents)+discount);
        return source with{Quantity=quantity,DiscountCents=discount,NetCents=net,TaxCents=tax,TotalCents=checked(net+tax),RoundingCents=rounding};
    }

    internal static string Signature(ClientLine line)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(line))));
    private static long Proportion(long cents,decimal quantity,decimal total)=>checked((long)decimal.Round(cents*quantity/total,0,MidpointRounding.AwayFromZero));

    private static long Share(long cents,long used,decimal quantity,decimal total,bool final)
    {
        long available=checked(cents-used);
        if(available<0)throw new ArgumentException("Earlier invoice portions exceed the source charge.");
        return final?available:Math.Min(available,Proportion(cents,quantity,total));
    }

    internal static void ValidateAmendment(DbWork db,OwnerSession owner,DocumentVersion quote)
    {
        var ids=db.Read("""
            SELECT DISTINCT C.ChargeId FROM InvoiceCoverage C JOIN DocumentVersions V ON V.Id=C.InvoiceVersionId
            WHERE C.BookingId=$booking AND V.Status<>'Void';
            """,r=>r.GetString(0),("$booking",quote.BookingId));
        foreach(string id in ids)
        {
            var source=quote.Snapshot.Lines.SingleOrDefault(l=>l.ChargeId==id);
            if(source is null || Existing(db,quote.BookingId,id).Any(c=>c.Signature!=Signature(source)))
                throw new ArgumentException("Credit previously billed charges before removing or changing them. Add a new source charge for additional services.");
        }
        var prior=DocumentVersions.List(db,owner).Where(v=>v.BookingId==quote.BookingId && v.Snapshot.Kind==DocumentKind.Invoice && v.Status!="Void");
        foreach(var group in prior.SelectMany(v=>v.Snapshot.Milestones).GroupBy(m=>m.Kind))
            if(group.Sum(m=>m.Cents)>quote.Snapshot.Milestones.Where(m=>m.Kind==group.Key).Sum(m=>m.Cents))
                throw new ArgumentException("The amended plan cannot reduce an already issued milestone share without a credit adjustment.");
    }

    internal static ClientMilestone[] Milestones(DbWork db,OwnerSession owner,DocumentVersion quote,long invoiceTotal)
    {
        var previous=DocumentVersions.List(db,owner).Where(v=>v.BookingId==quote.BookingId && v.Snapshot.Kind==DocumentKind.Invoice && v.Status!="Void").ToList();
        long[] available=quote.Snapshot.Milestones.Select(m=>checked(m.Cents-previous.SelectMany(v=>v.Snapshot.Milestones).Where(p=>p.Kind==m.Kind).Sum(p=>p.Cents))).ToArray();
        if(available.Any(a=>a<0) || invoiceTotal>available.Sum())throw new ArgumentException("Invoice shares exceed the accepted booking payment plan.");
        if(invoiceTotal==0)return [];
        var shares=PaymentPlan.Allocate(invoiceTotal,available);
        return quote.Snapshot.Milestones.Select((m,i)=>m with{Cents=shares[i]}).Where(m=>m.Cents>0).ToArray();
    }

    internal static void Save(DbWork db,DocumentVersion quote,DocumentVersion invoice,string payerId)
    {
        db.Execute("INSERT INTO InvoiceHeaders (VersionId,CustomerId,SourceQuoteVersionId) VALUES ($id,$payer,$quote);",("$id",invoice.Id),("$payer",payerId),("$quote",quote.Id));
        foreach(var line in invoice.Snapshot.Lines)
            SaveLine(db,quote,invoice.Id,line);
        foreach(var milestone in invoice.Snapshot.Milestones)
            db.Execute("INSERT INTO InvoiceMilestones (Id,InvoiceVersionId,Kind,Cents,DueDate) VALUES ($id,$invoice,$kind,$cents,$due);",
                ("$id",Guid.NewGuid().ToString()),("$invoice",invoice.Id),("$kind",milestone.Kind),("$cents",milestone.Cents),("$due",milestone.DueDate?.ToString("yyyy-MM-dd")));
        db.Execute("INSERT INTO BillingState (BookingId,Generation) VALUES ($id,1) ON CONFLICT(BookingId) DO UPDATE SET Generation=Generation+1;",("$id",quote.BookingId));
    }

    private static void SaveLine(DbWork db,DocumentVersion quote,string invoiceId,ClientLine line)=>db.Execute("""
        INSERT INTO InvoiceCoverage (Id,InvoiceVersionId,BookingId,ChargeId,Quantity,NetCents,TaxCents,DiscountCents,SourceSignature)
        VALUES ($id,$invoice,$booking,$charge,$quantity,$net,$tax,$discount,$signature);
        """,("$id",Guid.NewGuid().ToString()),("$invoice",invoiceId),("$booking",quote.BookingId),("$charge",line.ChargeId),
        ("$quantity",line.Quantity.ToString(CultureInfo.InvariantCulture)),("$net",line.NetCents),("$tax",line.TaxCents),("$discount",line.DiscountCents),
        ("$signature",Signature(quote.Snapshot.Lines.Single(l=>l.ChargeId==line.ChargeId))));
}
