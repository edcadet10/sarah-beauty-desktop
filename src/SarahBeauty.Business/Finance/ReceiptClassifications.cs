using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class ReceiptClassifications
{
    internal static FinancialDraft Prepare(DbWork db,OwnerSession owner,FinancialDraft draft)
    {
        if(draft.Kind!=FinancialKind.ReceiptClassification)return draft;
        var source=CorrectionRules.Original(db,owner,draft.OriginalId);
        if(!IsHeld(db,source) || draft.Date is null || draft.Date<source.Date
            || db.Scalar("SELECT Id FROM Movements WHERE ReversesId=$id;",("$id",draft.OriginalId)) is not null)
            throw new ArgumentException("Choose an unreversed held deposit or unclassified receipt, and a classification date on or after receipt.");
        if(source.CustomerId.Length>0 && draft.CustomerId.Length>0 && draft.CustomerId!=source.CustomerId)
            throw new ArgumentException("Retain the original payer when classifying a held customer deposit.");
        return draft with{CashAccountId=source.CashAccountId,PaymentMethod=source.PaymentMethod,
            CustomerId=source.CustomerId.Length>0?source.CustomerId:draft.CustomerId};
    }

    internal static FinancialDraft Review(DbWork db,OwnerSession owner,FinancialDraft draft)
    {
        draft=Prepare(db,owner,draft);
        var available=ValueAvailability.Payment(db,owner,draft.OriginalId,draft.Date!.Value);
        if(draft.AmountCents<=0 || draft.AmountCents>available.Cents)throw new ArgumentException("Choose a positive amount within the source receipt's remaining unclassified or held value.");
        long tax=draft.AmountCents==available.Cents?available.TaxCents:Money.Proportion(available.TaxCents,draft.AmountCents,available.Cents);
        return draft with{SourcePaymentGeneration=ValueState.Generation(db,owner,"Payment",draft.OriginalId),SourceTaxCents=tax};
    }

    internal static List<JournalLine> Lines(DbWork db,OwnerSession owner,FinancialDraft draft,List<Account> accounts)
    {
        if(draft.SourcePaymentGeneration!=ValueState.Generation(db,owner,"Payment",draft.OriginalId))throw new ArgumentException("The source receipt changed. Save and review this classification again.");
        if(draft.ReliableReference || string.IsNullOrWhiteSpace(draft.EvidenceId))throw new ArgumentException("Keep the payment's unique reference on its original movement and record evidence for this classification decision.");
        var reviewed=Review(db,owner,draft);
        if(draft.SourceTaxCents!=reviewed.SourceTaxCents || draft.TaxCents<draft.SourceTaxCents)
            throw new ArgumentException("Retain the tax already collected on this share. A tax reduction needs a separate reviewed refund or correction.");
        var source=CorrectionRules.Original(db,owner,draft.OriginalId);
        string Code(string code)=>accounts.Single(a=>a.Code==code && a.Active).Id;
        return new List<JournalLine>{new(Code(source.Kind==FinancialKind.UnclassifiedReceipt?"2400":"2200"),draft.AmountCents-draft.SourceTaxCents,0),
            new(draft.CounterAccountId,0,draft.AmountCents-draft.TaxCents),new(Code("2000"),0,draft.TaxCents-draft.SourceTaxCents)}
            .Where(l=>l.DebitCents>0 || l.CreditCents>0).ToList();
    }

    internal static bool IsHeld(DbWork db,FinancialDraft source)=>source.Kind==FinancialKind.UnclassifiedReceipt || source.Kind==FinancialKind.Receipt
        && db.Scalar("SELECT Code FROM Accounts WHERE Id=$id;",("$id",source.CounterAccountId)) as string=="2200";

    internal static bool AllowsEmpty(DbWork db,OwnerSession owner,FinancialDraft draft)=>draft.Kind==FinancialKind.ReceiptClassification
        || draft.Kind==FinancialKind.Reversal && CorrectionRules.Original(db,owner,draft.OriginalId).Kind==FinancialKind.ReceiptClassification;

    internal static ValueBalance Consumed(DbWork db,string payment,DateOnly date)
    {
        var rows=db.Read("""
            SELECT M.GrossCents,COALESCE(json_extract(M.DataJson,'$.SourceTaxCents'),0) FROM Movements M
            WHERE M.OriginalId=$id AND M.Kind='ReceiptClassification' AND M.Date<=$date
            AND NOT EXISTS(SELECT 1 FROM Movements R WHERE R.ReversesId=M.Id AND R.Date<=$date);
            """,r=>(Cents:r.GetInt64(0),Tax:r.GetInt64(1)),("$id",payment),("$date",date.ToStorageDate()));
        return new(rows.Sum(r=>r.Cents),rows.Sum(r=>r.Tax));
    }

    internal static void AfterPosting(DbWork db,OwnerSession owner,FinancialDraft draft)
    {
        string? root=draft.Kind==FinancialKind.ReceiptClassification?draft.OriginalId:null;
        if(draft.Kind==FinancialKind.Reversal)
        {
            var original=CorrectionRules.Original(db,owner,draft.OriginalId);
            if(original.Kind==FinancialKind.ReceiptClassification)root=original.OriginalId;
        }
        if(root is null)return;
        ValueAvailability.ValidatePaymentHistory(db,owner,root);
        ValueState.Advance(db,owner,"Payment",root);
    }
}
