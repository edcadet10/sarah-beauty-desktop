using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class ValueAvailability
{
    internal static ValueBalance Payment(DbWork db,OwnerSession owner,string paymentId,DateOnly date)=>Payment(db,owner.BusinessId,paymentId,date);
    internal static ValueBalance Payment(DbWork db,string businessId,string paymentId,DateOnly date)
    {
        var source=CorrectionRules.Original(db,businessId,paymentId);
        if(source.Kind is not (FinancialKind.Receipt or FinancialKind.UnclassifiedReceipt or FinancialKind.ReceiptClassification))throw new ArgumentException("Choose an approved customer receipt or reviewed classification.");
        string at=date.ToString("yyyy-MM-dd");
        bool reversed=db.Scalar("SELECT Id FROM Movements WHERE ReversesId=$id AND Date<=$date;",("$id",paymentId),("$date",at)) is not null;
        long gross=source.Date<=date && !reversed?source.AmountCents:0;
        long tax=source.Date<=date && !reversed?source.TaxCents:0;
        var refunds=db.Read("""
            SELECT M.GrossCents,M.TaxCents FROM Movements M WHERE M.OriginalId=$id AND M.Kind='Refund' AND M.Date<=$date
            AND NOT EXISTS(SELECT 1 FROM Movements R WHERE R.ReversesId=M.Id AND R.Date<=$date);
            """,r=>(Gross:r.GetInt64(0),Tax:r.GetInt64(1)),("$id",paymentId),("$date",at));
        var applied=ApplicationSum(db,"PaymentId",paymentId,date);
        var funding=Funding(db,"PaymentId",paymentId,date);
        var classified=ReceiptClassifications.Consumed(db,paymentId,date);
        return new(checked(gross-refunds.Sum(r=>r.Gross)-applied.Cents-funding.Cents-classified.Cents),
            checked(tax-refunds.Sum(r=>r.Tax)-applied.TaxCents-funding.TaxCents-classified.TaxCents));
    }

    internal static ValueBalance ApplicationSum(DbWork db,string column,string id,DateOnly date)
    {
        if(column is not ("PaymentId" or "CreditId" or "MilestoneId" or "InvoiceVersionId"))throw new ArgumentException("Unsupported application dimension.");
        var rows=db.Read($"SELECT Cents,TaxCents,ReversesId FROM PaymentApplications WHERE {column}=$id AND Date<=$date;",
            r=>(Cents:r.GetInt64(0),Tax:r.GetInt64(1),Reverse:!r.IsDBNull(2)),("$id",id),("$date",date.ToString("yyyy-MM-dd")));
        return new(rows.Sum(r=>r.Reverse?-r.Cents:r.Cents),rows.Sum(r=>r.Reverse?-r.Tax:r.Tax));
    }

    internal static ValueBalance Funding(DbWork db,string column,string id,DateOnly date)
    {
        if(column is not ("PaymentId" or "CreditId"))throw new ArgumentException("Unsupported funding dimension.");
        var rows=db.Read($"SELECT Kind,Cents,TaxCents FROM CreditFunding WHERE {column}=$id AND Date<=$date;",
            r=>(Reserve:r.GetString(0)=="Reserve",Cents:r.GetInt64(1),Tax:r.GetInt64(2)),("$id",id),("$date",date.ToString("yyyy-MM-dd")));
        return new(rows.Sum(r=>r.Reserve?r.Cents:-r.Cents),rows.Sum(r=>r.Reserve?r.Tax:-r.Tax));
    }

    internal static List<InvoiceDue> Invoice(DbWork db,OwnerSession owner,string invoiceId,DateOnly date)=>Invoice(db,owner.BusinessId,invoiceId,date);
    internal static List<InvoiceDue> Invoice(DbWork db,string businessId,string invoiceId,DateOnly date)
    {
        var invoice=DocumentVersions.Find(db,businessId,id:invoiceId)??throw new ArgumentException("Invoice not found.");
        if(invoice.Snapshot.Kind!=DocumentKind.Invoice || invoice.Status=="Void")throw new ArgumentException("Choose a current issued invoice.");
        string payer=(string)db.Scalar("SELECT CustomerId FROM InvoiceHeaders WHERE VersionId=$id;",("$id",invoiceId))!;
        var milestones=db.Read("SELECT Id,Kind,Cents,DueDate FROM InvoiceMilestones WHERE InvoiceVersionId=$id ORDER BY CASE Kind WHEN 'Trial' THEN 0 WHEN 'Deposit' THEN 1 ELSE 2 END;",
            r=>(Id:r.GetString(0),Kind:r.GetString(1),Cents:r.GetInt64(2),Due:r.IsDBNull(3)?(DateOnly?)null:DateOnly.Parse(r.GetString(3))),("$id",invoiceId));
        if(date<invoice.Snapshot.IssueDate)return [];
        long[] taxes=milestones.Count==0?[]:PaymentPlan.Allocate(invoice.Snapshot.TaxCents,milestones.Select(m=>m.Cents).ToArray());
        return milestones.Select((m,i)=>
        {
            var applied=ApplicationSum(db,"MilestoneId",m.Id,date);
            var reduced=CreditNoteReductions.Milestone(db,m.Id,date);
            return new InvoiceDue(invoice.Id,invoice.Snapshot.Number,payer,invoice.Snapshot.Client.Name,m.Id,m.Kind,m.Due??invoice.Snapshot.DueDate,
                checked(m.Cents-applied.Cents-reduced.Cents),checked(taxes[i]-applied.TaxCents-reduced.TaxCents));
        }).ToList();
    }

    internal static void ValidatePaymentHistory(DbWork db,OwnerSession owner,string paymentId)
    {
        var dates=db.Read("""
            SELECT Date FROM Movements WHERE Id=$id OR OriginalId=$id OR ReversesId=$id OR ReversesId IN(SELECT Id FROM Movements WHERE OriginalId=$id)
            UNION SELECT Date FROM PaymentApplications WHERE PaymentId=$id
            UNION SELECT Date FROM CreditFunding WHERE PaymentId=$id ORDER BY Date;
            """,r=>DateOnly.Parse(r.GetString(0)),("$id",paymentId));
        foreach(var date in dates)Validate(Payment(db,owner,paymentId,date),"Receipt availability",date);
    }

    internal static void ValidateInvoiceHistory(DbWork db,OwnerSession owner,string invoiceId)
    {
        var dates=db.Read("SELECT Date FROM PaymentApplications WHERE InvoiceVersionId=$id UNION SELECT Date FROM CreditNoteHeaders WHERE InvoiceVersionId=$id ORDER BY Date;",
            r=>DateOnly.Parse(r.GetString(0)),("$id",invoiceId));
        foreach(var date in dates)
            foreach(var due in Invoice(db,owner,invoiceId,date))Validate(new(due.Cents,due.TaxCents),"Invoice/milestone balance",date);
    }

    internal static void Validate(ValueBalance value,string label,DateOnly date)
    {
        if(value.Cents<0 || value.TaxCents<0 || value.TaxCents>value.Cents)
            throw new ArgumentException($"{label} would become invalid on {date:yyyy-MM-dd}. Review the amount and tax classification.");
    }
}
