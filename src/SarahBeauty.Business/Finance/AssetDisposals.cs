using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class AssetDisposals
{
    internal static bool IsDisposed(DbWork db,OwnerSession owner,string asset,DateOnly date)=>db.Scalar("""
        SELECT M.Id FROM Movements M WHERE M.BusinessId=$business AND M.AssetId=$asset AND M.Kind='AssetDisposal' AND M.Date<=$date
        AND NOT EXISTS(SELECT 1 FROM Movements R WHERE R.ReversesId=M.Id AND R.Date<=$date) LIMIT 1;
        """,("$business",owner.BusinessId),("$asset",asset),("$date",date.ToString("yyyy-MM-dd"))) is not null;

    internal static List<JournalLine> Lines(DbWork db,OwnerSession owner,FinancialDraft draft,List<Account> accounts)
    {
        if(draft.Kind!=FinancialKind.AssetDisposal || string.IsNullOrWhiteSpace(draft.EvidenceId))
            throw new ArgumentException("Record the approved disposal decision and evidence.");
        if(draft.AssetGeneration!=ValueState.Generation(db,owner,"Asset",draft.AssetId))
            throw new ArgumentException("The asset ledger changed. Save and review this disposal again.");
        if(db.Scalar("SELECT M.Id FROM Movements M JOIN JournalLines J ON J.MovementId=M.Id WHERE M.BusinessId=$business AND J.AssetId=$asset AND M.Date>$date LIMIT 1;",
            ("$business",owner.BusinessId),("$asset",draft.AssetId),("$date",draft.Date!.Value.ToString("yyyy-MM-dd"))) is not null)
            throw new ArgumentException("The disposal date precedes another asset movement. Review the asset history first.");
        var values=Values(db,owner,draft);
        long cost=values.Where(v=>v.Code!="1590").Sum(v=>v.Cents);
        long depreciation=-values.Where(v=>v.Code=="1590").Sum(v=>v.Cents);
        if(cost<=0 || depreciation<0 || depreciation>cost || values.Any(v=>v.Code!="1590" && v.Cents<0))
            throw new ArgumentException("Approve a valid asset book value and review accumulated depreciation before disposal. Unknown cost is not zero.");
        long carrying=checked(cost-depreciation);
        long proceeds=checked(draft.AmountCents-draft.TaxCents);
        string Code(string code)=>accounts.Single(a=>a.Code==code && a.Active).Id;
        var lines=values.Where(v=>v.Cents>0).Select(v=>new JournalLine(v.AccountId,0,v.Cents)).ToList();
        if(depreciation>0)lines.Add(new(Code("1590"),depreciation,0));
        if(draft.AmountCents>0)lines.Add(new(draft.CashAccountId,draft.AmountCents,0));
        if(draft.TaxCents>0)lines.Add(new(Code("2000"),0,draft.TaxCents));
        if(proceeds>carrying)lines.Add(new(Code("4010"),0,proceeds-carrying));
        if(proceeds<carrying)lines.Add(new(Code("5060"),carrying-proceeds,0));
        return lines;
    }

    private static List<AssetValue> Values(DbWork db,OwnerSession owner,FinancialDraft draft)=>db.Read("""
        SELECT A.Id,A.Code,SUM(J.DebitCents-J.CreditCents) FROM Movements M JOIN JournalLines J ON J.MovementId=M.Id
        JOIN Accounts A ON A.Id=J.AccountId WHERE M.BusinessId=$business AND J.AssetId=$asset AND M.Date<=$date
        AND A.Type='Asset' AND A.CashKind='None' GROUP BY A.Id,A.Code;
        """,r=>new AssetValue(r.GetString(0),r.GetString(1),r.GetInt64(2)),("$business",owner.BusinessId),
        ("$asset",draft.AssetId),("$date",draft.Date!.Value.ToString("yyyy-MM-dd")));

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

    private sealed record AssetValue(string AccountId,string Code,long Cents);
}
