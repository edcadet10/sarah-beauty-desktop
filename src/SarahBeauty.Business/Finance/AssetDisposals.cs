using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class AssetDisposals
{
    internal static bool IsDisposed(DbWork db,OwnerSession owner,string asset,DateOnly date)=>IsDisposed(db,owner.BusinessId,asset,date);
    internal static bool IsDisposed(DbWork db,string businessId,string asset,DateOnly date)=>db.Scalar("""
        SELECT M.Id FROM Movements M WHERE M.BusinessId=$business AND M.Kind<>'Reversal' AND M.Date<=$date
        AND ((M.Kind='AssetDisposal' AND M.AssetId=$asset) OR EXISTS(
            SELECT 1 FROM json_each(M.DataJson,'$.AssetAdjustments') C WHERE json_extract(C.value,'$.AssetId')=$asset
            AND json_extract(C.value,'$.Treatment')='Retire returned asset'))
        AND NOT EXISTS(SELECT 1 FROM Movements R WHERE R.ReversesId=M.Id AND R.Date<=$date) LIMIT 1;
        """,("$business",businessId),("$asset",asset),("$date",date.ToStorageDate())) is not null;

    internal static List<JournalLine> Lines(DbWork db,OwnerSession owner,FinancialDraft draft,List<Account> accounts)
    {
        if(draft.Kind!=FinancialKind.AssetDisposal || string.IsNullOrWhiteSpace(draft.EvidenceId))
            throw new ArgumentException("Record the approved disposal decision and evidence.");
        if(draft.AssetGeneration!=ValueState.Generation(db,owner,"Asset",draft.AssetId))
            throw new ArgumentException("The asset ledger changed. Save and review this disposal again.");
        AssetRetirements.CheckLaterMovement(db,owner,draft.AssetId,draft.Date!.Value);
        var book=AssetBookValue.Read(db,owner,draft.AssetId,draft.Date.Value);
        long proceeds=checked(draft.AmountCents-draft.TaxCents);
        string Code(string code)=>accounts.Single(a=>a.Code==code && a.Active).Id;
        var lines=book.Reduce(book.Cost,draft.AssetId);
        if(book.Depreciation>0)lines.Add(new(Code("1590"),book.Depreciation,0,draft.AssetId));
        if(draft.AmountCents>0)lines.Add(new(draft.CashAccountId,draft.AmountCents,0));
        if(draft.TaxCents>0)lines.Add(new(Code("2000"),0,draft.TaxCents));
        if(proceeds>book.Carrying)lines.Add(new(Code("4010"),0,proceeds-book.Carrying,draft.AssetId));
        if(proceeds<book.Carrying)lines.Add(new(Code("5060"),book.Carrying-proceeds,0,draft.AssetId));
        return lines;
    }

    internal static bool AllowsEmpty(DbWork db,OwnerSession owner,FinancialDraft draft)=>
        draft.Kind==FinancialKind.AssetDisposal && draft.AmountCents==0 || draft.Kind==FinancialKind.Reversal
        && CorrectionRules.Original(db,owner,draft.OriginalId) is {Kind:FinancialKind.AssetDisposal,AmountCents:0};

    internal static void AfterPosting(DbWork db,BusinessStore store,OwnerSession owner,FinancialDraft draft,string movement)
    {
        if(draft.Kind!=FinancialKind.AssetDisposal && draft.Kind!=FinancialKind.Reversal)return;
        if(draft.Kind==FinancialKind.Reversal && CorrectionRules.Original(db,owner,draft.OriginalId).Kind!=FinancialKind.AssetDisposal)return;
        var records=new RecordStore<Asset>(store,"Asset");
        var asset=records.Get(db,owner,draft.AssetId);
        if(draft.Kind==FinancialKind.Reversal && asset.DisposalMovementId!=draft.OriginalId)
            throw new ArgumentException("Resolve the asset's later disposal before reversing this one.");
        records.Save(db,owner,asset with{DisposedDate=draft.Kind==FinancialKind.AssetDisposal?draft.Date:null,
            DisposalMovementId=draft.Kind==FinancialKind.AssetDisposal?movement:""});
    }

}
