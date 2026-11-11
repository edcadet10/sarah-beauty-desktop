using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal sealed record ApplicationValueSource(string CustomerId,ValueBalance Available);

internal static class ApplicationSource
{
    internal static (string Kind,string Id) Key(PaymentApplicationDraft draft)=>
        draft.CreditId.Length>0?("Credit",draft.CreditId):("Payment",draft.PaymentId);

    internal static ApplicationValueSource Read(DbWork db,OwnerSession owner,PaymentApplicationDraft draft)
    {
        if((draft.PaymentId.Length>0)==(draft.CreditId.Length>0))throw new ArgumentException("Choose exactly one approved receipt or funded customer credit.");
        var credit=draft.CreditId.Length>0?CreditValues.Get(db,owner,draft.CreditId):null;
        string payment=credit?.PaymentId??draft.PaymentId;
        var receipt=CorrectionRules.Original(db,owner,payment);
        string? accountType=db.Scalar("SELECT Type FROM Accounts WHERE Id=$id;",("$id",receipt.CounterAccountId)) as string;
        if(receipt.Kind is not (FinancialKind.Receipt or FinancialKind.ReceiptClassification) || accountType!="Income")
            throw new ArgumentException("Use an approved income receipt or a credit funded by one. Held money needs a reviewed reclassification first.");
        if((credit?.IssueDate??receipt.Date)>draft.Date)throw new ArgumentException("The application cannot precede its source value.");
        var available=credit is null?ValueAvailability.Payment(db,owner,payment,draft.Date!.Value):CreditValues.Available(db,owner,credit.Id,draft.Date!.Value);
        return new(credit?.CustomerId??receipt.CustomerId,available);
    }
}
