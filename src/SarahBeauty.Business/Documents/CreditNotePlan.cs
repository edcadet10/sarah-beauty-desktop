using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

internal static class CreditNotePlan
{
    internal static (DocumentVersion Invoice,ClientSnapshot Snapshot,List<MilestoneReduction> Reductions) Build(
        DbWork db,OwnerSession owner,CreditNoteDraft draft,string number)
    {
        var invoice=DocumentVersions.Find(db,owner,id:draft.InvoiceVersionId)??throw new ArgumentException("Choose an issued invoice.");
        if(invoice.Snapshot.Kind!=DocumentKind.Invoice || invoice.Status=="Void" || draft.IssueDate is null
            || draft.IssueDate<invoice.Snapshot.IssueDate || string.IsNullOrWhiteSpace(draft.Name))
            throw new ArgumentException("Choose an invoice, a valid credit date, and a client-facing reason.");
        PostingValidation.CheckClosedPeriod(db,owner,draft.IssueDate.Value);
        if(draft.InvoiceGeneration!=ValueState.Generation(db,owner,"Invoice",invoice.Id))
            throw new ArgumentException("The invoice balance changed. Save and review this credit note again.");
        if(draft.Portions.Length==0 || draft.Portions.Select(p=>p.ChargeId).Distinct().Count()!=draft.Portions.Length)
            throw new ArgumentException("Choose distinct original invoice charges and quantities.");
        var lines=draft.Portions.Select(p=>Portion(db,invoice,p)).ToArray();
        var dues=ValueAvailability.Invoice(db,owner,invoice.Id,draft.IssueDate.Value);
        var reductions=Distribute(dues,lines.Sum(l=>l.NetCents),lines.Sum(l=>l.TaxCents));
        var snapshot=invoice.Snapshot with{Kind=DocumentKind.CreditNote,Number=number,Version=1,IssueDate=draft.IssueDate.Value,
            DueDate=null,Terms=draft.Name.Trim(),Lines=lines,NetCents=lines.Sum(l=>l.NetCents),TaxCents=lines.Sum(l=>l.TaxCents),
            TotalCents=lines.Sum(l=>l.TotalCents),RelatedDocument=invoice.Snapshot.Number,
            Milestones=reductions.Select(r=>new ClientMilestone(r.Kind,r.Cents,null)).ToArray()};
        return(invoice,snapshot,reductions);
    }

    private static ClientLine Portion(DbWork db,DocumentVersion invoice,InvoicePortion portion)
    {
        var line=invoice.Snapshot.Lines.SingleOrDefault(l=>l.ChargeId==portion.ChargeId)??throw new ArgumentException("Choose a charge from the original invoice.");
        var prior=CreditNoteReductions.Lines(db,invoice.Id,line.ChargeId);
        decimal available=line.Quantity-prior.Sum(p=>p.Quantity);
        if(portion.Quantity<=0 || portion.Quantity>available)throw new ArgumentException("The credit quantity exceeds the original invoice's remaining quantity.");
        bool final=portion.Quantity==available;
        long net=Share(line.NetCents,prior.Sum(p=>p.NetCents),portion.Quantity,line.Quantity,final);
        long tax=Share(line.TaxCents,prior.Sum(p=>p.TaxCents),portion.Quantity,line.Quantity,final);
        long discount=Share(line.DiscountCents,prior.Sum(p=>p.DiscountCents),portion.Quantity,line.Quantity,final);
        return line with{Quantity=portion.Quantity,NetCents=net,TaxCents=tax,DiscountCents=discount,TotalCents=checked(net+tax),
            RoundingCents=checked(net-Money.Multiply(portion.Quantity,line.UnitCents)+discount)};
    }

    private static long Share(long total,long used,decimal quantity,decimal whole,bool final)=>
        final?checked(total-used):Math.Min(checked(total-used),checked((long)decimal.Round(total*quantity/whole,0,MidpointRounding.AwayFromZero)));

    private static List<MilestoneReduction> Distribute(List<InvoiceDue> dues,long net,long tax)
    {
        long[] nets=Allocate(net,dues.Select(d=>d.Cents-d.TaxCents).ToArray());
        long[] taxes=Allocate(tax,dues.Select(d=>d.TaxCents).ToArray());
        return dues.Select((d,i)=>new MilestoneReduction(d.MilestoneId,d.Kind,checked(nets[i]+taxes[i]),taxes[i])).Where(r=>r.Cents>0).ToList();
    }

    private static long[] Allocate(long cents,long[] available)
    {
        if(available.Any(c=>c<0) || cents>available.Sum())
            throw new ArgumentException("Unapply the affected payment or credit before reducing paid charges. The credit note cannot exceed the unpaid gross/tax balance.");
        return cents==0?new long[available.Length]:PaymentPlan.Allocate(cents,available);
    }
}
