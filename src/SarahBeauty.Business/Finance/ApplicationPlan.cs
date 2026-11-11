using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class ApplicationPlan
{
    internal static List<PaymentApplication> Build(DbWork db,OwnerSession owner,PaymentApplicationDraft draft)
    {
        if(draft.Date is null || string.IsNullOrWhiteSpace(draft.Name))throw new ArgumentException("Choose an application date and reason.");
        Money.Positive(draft.Cents);
        PostingValidation.CheckClosedPeriod(db,owner,draft.Date.Value);
        if(draft.PaymentGeneration!=ValueState.Generation(db,owner,"Payment",draft.PaymentId)
            || draft.InvoiceGeneration!=ValueState.Generation(db,owner,"Invoice",draft.InvoiceVersionId))
            throw new ArgumentException("A source or target balance changed. Save and review this draft again.");
        return draft.ReversesId.Length>0?Reverse(db,owner,draft):Apply(db,owner,draft);
    }

    private static List<PaymentApplication> Apply(DbWork db,OwnerSession owner,PaymentApplicationDraft draft)
    {
        var source=CorrectionRules.Original(db,owner,draft.PaymentId);
        var account=db.Scalar("SELECT Type FROM Accounts WHERE Id=$id;",("$id",source.CounterAccountId)) as string;
        if(source.Kind!=FinancialKind.Receipt || account!="Income")throw new ArgumentException("Choose an approved service receipt. Held deposits need an approved reclassification first.");
        if(source.Date>draft.Date)throw new ArgumentException("An application cannot precede the actual receipt date.");
        var available=ValueAvailability.Payment(db,owner,draft.PaymentId,draft.Date!.Value);
        var dues=ValueAvailability.Invoice(db,owner,draft.InvoiceVersionId,draft.Date.Value);
        if(dues.Count==0)throw new ArgumentException("This invoice is not available on the selected date.");
        if(dues[0].CustomerId!=source.CustomerId && string.IsNullOrWhiteSpace(draft.OnBehalfReason))
            throw new ArgumentException("Record the explicit decision to pay on another customer's behalf.");
        var eligible=dues.Where(d=>draft.MilestoneId.Length==0 || d.MilestoneId==draft.MilestoneId).ToList();
        if(draft.Cents>available.Cents || draft.Cents>eligible.Sum(d=>d.Cents))
            throw new ArgumentException("The application exceeds the available receipt or invoice/milestone balance.");
        var rows=Distribute(draft,eligible);
        ValueAvailability.Validate(new(available.Cents-draft.Cents,available.TaxCents-rows.Sum(r=>r.TaxCents)),"Remaining receipt classification",draft.Date.Value);
        return rows;
    }

    private static List<PaymentApplication> Distribute(PaymentApplicationDraft draft,List<InvoiceDue> dues)
    {
        long left=draft.Cents;
        var rows=new List<PaymentApplication>();
        foreach(var due in dues.Where(d=>d.Cents>0))
        {
            long take=Math.Min(left,due.Cents);
            if(take==0)break;
            long tax=take==due.Cents?due.TaxCents:Math.Min(due.TaxCents,Money.Proportion(due.TaxCents,take,due.Cents));
            rows.Add(new(Guid.NewGuid().ToString(),"",draft.PaymentId,null,draft.InvoiceVersionId,due.MilestoneId,take,tax,draft.Date!.Value,null));
            left-=take;
        }
        if(left!=0)throw new ArgumentException("The selected milestone cannot receive this amount.");
        return rows;
    }

    private static List<PaymentApplication> Reverse(DbWork db,OwnerSession owner,PaymentApplicationDraft draft)
    {
        var original=ApplicationWriter.Original(db,owner,draft.ReversesId);
        if(original.ReversesId is not null || original.PaymentId is null || draft.Date<original.Date)
            throw new ArgumentException("Choose an original receipt application and a reversal date on or after it.");
        var reversed=db.Read("SELECT Cents,TaxCents FROM PaymentApplications WHERE ReversesId=$id;",r=>(Cents:r.GetInt64(0),Tax:r.GetInt64(1)),("$id",original.Id));
        long remaining=original.Cents-reversed.Sum(r=>r.Cents);
        long taxRemaining=original.TaxCents-reversed.Sum(r=>r.Tax);
        if(draft.Cents>remaining)throw new ArgumentException("The reversal exceeds this application's remaining amount.");
        long tax=draft.Cents==remaining?taxRemaining:Money.Proportion(taxRemaining,draft.Cents,remaining);
        return [new(Guid.NewGuid().ToString(),"",original.PaymentId,null,original.InvoiceVersionId,original.MilestoneId,
            draft.Cents,tax,draft.Date!.Value,original.Id)];
    }
}
