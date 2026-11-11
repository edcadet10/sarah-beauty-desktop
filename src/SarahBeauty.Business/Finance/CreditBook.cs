using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

public sealed class CreditBook(BusinessStore store)
{
    private readonly RecordStore<CreditDraft> _drafts=new(store,"CreditDraft");
    internal Action<string>? FailurePoint {get;set;}
    public List<CreditDraft> Drafts(OwnerSession owner)=>_drafts.List(owner);
    public List<CustomerCredit> Credits(OwnerSession owner)=>store.Read(db=>{store.RequireSession(db,owner);return CreditValues.List(db,owner);});
    public ValueBalance Available(OwnerSession owner,string id,DateOnly date)=>store.Read(db=>{store.RequireSession(db,owner);return CreditValues.Available(db,owner,id,date);});

    public CreditDraft SaveDraft(OwnerSession owner,CreditDraft draft)
    {
        draft=draft with{Id=draft.Id.Length==0?Guid.NewGuid().ToString():draft.Id};
        return store.Write(owner,"Save credit draft",draft.Id,db=>
        {
            if(draft.Status!="Draft" || (draft.Revision>0 && _drafts.Get(db,owner,draft.Id).Status!="Draft"))throw new ArgumentException("Approved credits require a linked movement.");
            if(draft.Action==CreditAction.ReturnToReceipt)draft=draft with{PaymentId=CreditValues.Get(db,owner,draft.CreditId).PaymentId};
            return _drafts.Save(db,owner,draft with{PaymentGeneration=ValueState.Generation(db,owner,"Payment",draft.PaymentId),
                CreditGeneration=draft.CreditId.Length==0?0:ValueState.Generation(db,owner,"Credit",draft.CreditId)});
        });
    }

    public CreditPreview Preview(OwnerSession owner,CreditDraft draft)=>store.Read(db=>{store.RequireSession(db,owner);return Preview(db,owner,draft);});

    private static CreditPreview Preview(DbWork db,OwnerSession owner,CreditDraft draft)
    {
        if(!Enum.IsDefined(draft.Action) || draft.Date is null || string.IsNullOrWhiteSpace(draft.Name))throw new ArgumentException("Choose a credit action, date, and explicit reason.");
        Money.Positive(draft.Cents);
        PostingValidation.CheckClosedPeriod(db,owner,draft.Date.Value);
        if(draft.PaymentGeneration!=ValueState.Generation(db,owner,"Payment",draft.PaymentId)
            || (draft.Action==CreditAction.ReturnToReceipt && draft.CreditGeneration!=ValueState.Generation(db,owner,"Credit",draft.CreditId)))
            throw new ArgumentException("Available value changed. Save and review the credit draft again.");
        var source=CorrectionRules.Original(db,owner,draft.PaymentId);
        string? accountType=db.Scalar("SELECT Type FROM Accounts WHERE Id=$id;",("$id",source.CounterAccountId)) as string;
        bool income=(source.Kind is FinancialKind.Receipt or FinancialKind.ReceiptClassification) && accountType=="Income";
        bool held=ReceiptClassifications.IsHeld(db,source);
        if((!income && !held) || source.Date>draft.Date)
            throw new ArgumentException("Choose an approved income, held deposit, or identified suspense receipt dated on or before this credit action.");
        var receipt=ValueAvailability.Payment(db,owner,draft.PaymentId,draft.Date.Value);
        var available=draft.Action==CreditAction.Issue?receipt:CreditValues.Available(db,owner,draft.CreditId,draft.Date.Value);
        if(draft.Cents>available.Cents)throw new ArgumentException("This action exceeds the available funded value.");
        long tax=Money.Proportion(available.TaxCents,draft.Cents,available.Cents);
        string customer=CustomerBook.Find(db,owner,source.CustomerId)?.FullName??throw new ArgumentException("Identify the actual payer before creating customer credit from suspense value.");
        string treatment=source.Kind==FinancialKind.UnclassifiedReceipt?"Unclassified suspense":income?"Recognized income":"Held customer deposit";
        return draft.Action==CreditAction.Issue?new(customer,draft.Cents,tax,new(receipt.Cents-draft.Cents,receipt.TaxCents-tax),new(draft.Cents,tax)){SourceTreatment=treatment}
            :new(customer,draft.Cents,tax,new(receipt.Cents+draft.Cents,receipt.TaxCents+tax),new(available.Cents-draft.Cents,available.TaxCents-tax)){SourceTreatment=treatment};
    }

    public CustomerCredit Approve(OwnerSession owner,string draftId,int expectedRevision,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);
            var draft=_drafts.Get(db,owner,draftId);
            if(draft.Status=="Posted")return CreditValues.Get(db,owner,draft.CreditId);
            if(draft.Revision!=expectedRevision)throw new ArgumentException("Reload the changed credit draft.");
            return Approve(db,owner,draft);
        });
    }

    internal CustomerCredit Approve(DbWork db,OwnerSession owner,CreditDraft draft)
    {
        var preview=Preview(db,owner,draft);
        string creditId=draft.Action==CreditAction.Issue?draft.Id:draft.CreditId;
        if(draft.Action==CreditAction.Issue)CreateCredit(db,owner,draft,preview.TaxCents);
        FailurePoint?.Invoke("credit-record");
        CreditValues.Fund(db,owner,creditId,draft.PaymentId,draft.Action==CreditAction.Issue?"Reserve":"Release",draft.Date!.Value,
            draft.Cents,preview.TaxCents,draft.Name,draft.Id);
        FailurePoint?.Invoke("credit-funding");
        ValueAvailability.ValidatePaymentHistory(db,owner,draft.PaymentId);
        CreditValues.ValidateHistory(db,owner,creditId);
        ValueState.Advance(db,owner,"Payment",draft.PaymentId);
        ValueState.Advance(db,owner,"Credit",creditId);
        _drafts.Save(db,owner,draft with{Status="Posted",CreditId=creditId});
        BusinessStore.Audit(db,owner,draft.Action==CreditAction.Issue?"Issue funded credit":"Return credit to receipt availability",creditId);
        return CreditValues.Get(db,owner,creditId);
    }

    private static void CreateCredit(DbWork db,OwnerSession owner,CreditDraft draft,long tax)
    {
        var source=CorrectionRules.Original(db,owner,draft.PaymentId);
        db.Execute("""
            INSERT INTO CustomerCredits (Id,BusinessId,CustomerId,PaymentId,IssueDate,IssuedCents,IssuedTaxCents,Reason,OwnerId,AtUtc)
            VALUES ($id,$business,$customer,$payment,$date,$cents,$tax,$reason,$owner,$at);
            """,("$id",draft.Id),("$business",owner.BusinessId),("$customer",source.CustomerId),("$payment",draft.PaymentId),
            ("$date",draft.Date!.Value.ToString("yyyy-MM-dd")),("$cents",draft.Cents),("$tax",tax),("$reason",draft.Name),
            ("$owner",owner.OwnerId),("$at",DateTimeOffset.UtcNow.ToString("O")));
    }
}
