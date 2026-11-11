using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Bookings;

internal sealed class CancellationExecution(BusinessStore store,OwnerSession owner,DbWork db,CancellationDraft draft)
{
    private readonly List<CancellationAction> _actions=[];
    private readonly List<JournalLine> _journal=[];
    internal Action<string>? FailurePoint {get;set;}

    internal CancellationPreview Run(CancellationSnapshot before)
    {
        CancellationRules.Validate(db,store,owner,draft,before);
        foreach(var item in draft.Unapplications)Unapply(before,item);FailurePoint?.Invoke("unapplications");
        foreach(var item in draft.Reductions)Reduce(before,item);FailurePoint?.Invoke("credit-notes");
        var unallocated=new CancellationSources(store).Load(db,owner,draft.BookingId,draft.Date!.Value);
        foreach(var value in draft.Values){CancellationRules.Limit(unallocated,value);Refund(value);Transfer(value);Retain(value);}
        FailurePoint?.Invoke("value-decisions");
        var book=new BookingBook(store);var current=book.Records.Get(db,owner,draft.BookingId);
        if(current.Status!="Cancelled")
        {
            book.Transition(db,owner,current,"Cancelled",draft.Name);
            BusinessStore.Audit(db,owner,"Cancel booking work",current.Id);
        }
        FailurePoint?.Invoke("booking-status");
        var after=new CancellationSources(store).Load(db,owner,draft.BookingId,draft.Date.Value);
        return new(draft,before,after,_actions.ToArray(),_journal.ToArray());
    }

    private T Save<T>(string kind,T value) where T:BusinessRecord=>new RecordStore<T>(store,kind).Save(db,owner,(T)(value with{Id=Guid.NewGuid().ToString()}));

    private void Unapply(CancellationSnapshot before,CancellationUnapply item)
    {
        var source=before.Applications.SingleOrDefault(a=>a.Id==item.ApplicationId && a.InvoiceBookingId==before.BookingId)
            ??throw new ArgumentException("Unapply an original payment application to this booking.");
        var key=source.CreditId.Length>0?("Credit",source.CreditId):("Payment",source.PaymentId);
        var value=Save("PaymentApplicationDraft",new PaymentApplicationDraft{Name=draft.Name,Date=draft.Date,ReversesId=source.Id,Cents=item.Cents,
            PaymentId=source.PaymentId,CreditId=source.CreditId,InvoiceVersionId=source.InvoiceVersionId,MilestoneId=source.MilestoneId,
            SourceGeneration=ValueState.Generation(db,owner,key.Item1,key.Item2),InvoiceGeneration=ValueState.Generation(db,owner,"Invoice",source.InvoiceVersionId)});
        var rows=new PaymentApplications(store).Approve(db,owner,value);
        _actions.Add(new("Unapply",source.Id,rows[0].Id,rows.Sum(r=>r.Cents),rows.Sum(r=>r.TaxCents)));
    }

    private void Reduce(CancellationSnapshot before,CancellationReduction item)
    {
        if(!before.Documents.Any(d=>d.Id==item.InvoiceVersionId && d.BookingId==before.BookingId && d.Snapshot.Kind==DocumentKind.Invoice))
            throw new ArgumentException("Reduce an original invoice from this booking.");
        var value=Save("CreditNoteDraft",new CreditNoteDraft{Name=draft.ClientReason,IssueDate=draft.Date,InvoiceVersionId=item.InvoiceVersionId,Portions=item.Portions,
            InvoiceGeneration=ValueState.Generation(db,owner,"Invoice",item.InvoiceVersionId)});
        var note=new CreditNoteBook(store).Issue(db,owner,value);
        _actions.Add(new("Credit note",item.InvoiceVersionId,note.Id,note.Snapshot.TotalCents,note.Snapshot.TaxCents));
    }

    private void Refund(CancellationValueChoice choice)
    {
        if(choice.RefundCents==0)return;
        var source=CorrectionRules.Original(db,owner,choice.PaymentId);
        string classification=source.Kind==FinancialKind.UnclassifiedReceipt?ChartOfAccounts.List(db,owner).Single(a=>a.Code=="2400").Id:source.CounterAccountId;
        var value=Save("FinancialDraft",new FinancialDraft{Name=draft.Name,Kind=FinancialKind.Refund,Date=draft.Date,AmountCents=choice.RefundCents,
            TaxCents=choice.RefundTaxCents,TaxResolved=true,OriginalId=choice.PaymentId,CreditId=choice.CreditId,CustomerId=source.CustomerId,
            CashAccountId=choice.RefundCashAccountId,CounterAccountId=classification,PaymentMethod=choice.RefundMethod,EvidenceId=choice.RefundEvidenceId,TaxRefundAllocation=choice.TaxRefundAllocation});
        var posted=new FinanceBook(store).Post(db,owner,value);
        _journal.AddRange(db.Read("SELECT AccountId,DebitCents,CreditCents FROM JournalLines WHERE MovementId=$id;",r=>new JournalLine(r.GetString(0),r.GetInt64(1),r.GetInt64(2)),("$id",posted.Id)));
        _actions.Add(new("Refund",choice.PaymentId,posted.Id,choice.RefundCents,choice.RefundTaxCents));FailurePoint?.Invoke("refund");
    }

    private void Transfer(CancellationValueChoice choice)
    {
        if(choice.TransferCents==0)return;
        CustomerCredit credit;
        if(choice.CreditId.Length>0)credit=CreditValues.Get(db,owner,choice.CreditId);
        else
        {
            var value=Save("CreditDraft",new CreditDraft{Name=draft.Name,Date=draft.Date,PaymentId=choice.PaymentId,Cents=choice.TransferCents,
                PaymentGeneration=ValueState.Generation(db,owner,"Payment",choice.PaymentId)});
            credit=new CreditBook(store).Approve(db,owner,value);
        }
        var available=CreditValues.Available(db,owner,credit.Id,draft.Date!.Value);
        long tax=SarahBeauty.Business.Rules.Money.Proportion(available.TaxCents,choice.TransferCents,available.Cents);
        _actions.Add(new("Transfer credit",choice.PaymentId,credit.Id,choice.TransferCents,tax,choice.DestinationBookingId));FailurePoint?.Invoke("credit");
    }

    private void Retain(CancellationValueChoice choice)
    {
        if(choice.RetainCents>0)_actions.Add(new("Retain source treatment",choice.CreditId.Length>0?choice.CreditId:choice.PaymentId,"",choice.RetainCents,0));
    }
}
