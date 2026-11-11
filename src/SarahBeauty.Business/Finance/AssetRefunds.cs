using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class AssetRefunds
{
    internal static void ValidateContext(FinancialDraft draft)
    {
        if(draft.AssetAdjustments.Length==0)return;
        if(draft.Kind is not (FinancialKind.SupplierRefund or FinancialKind.Reversal))throw new ArgumentException("Use a supported asset-refund adjustment workflow.");
        if(draft.AssetAdjustments.Select(c=>c.AssetId).Distinct().Count()!=draft.AssetAdjustments.Length
            || draft.AssetAdjustments.Any(c=>c.Treatment is not (AssetValueChoice.Reduce or AssetValueChoice.Recover or AssetValueChoice.Retire)))
            throw new ArgumentException("Choose distinct assets and a supported refund treatment.");
    }

    internal static List<JournalLine> ClassicLines(DbWork db,OwnerSession owner,FinancialDraft draft,List<Account> accounts)
    {
        var account=accounts.Single(a=>a.Id==draft.CounterAccountId);
        if(draft.AssetAdjustments.Any(c=>c.AssetId!=draft.AssetId) || (draft.AssetAdjustments.Length>0 && account.Type!="Asset"))
            throw new ArgumentException("The refund adjustment must identify its original individual asset.");
        var choice=draft.AssetAdjustments.SingleOrDefault();
        if(choice is null || choice.Treatment==AssetValueChoice.Reduce)return PostingRules.Build(draft,accounts);
        var lines=ValueLines(db,owner,draft,draft.AssetId,draft.AmountCents,choice,accounts);
        lines.Add(new(draft.CashAccountId,draft.AmountCents,0));
        return lines;
    }

    internal static List<JournalLine> ValueLines(DbWork db,OwnerSession owner,FinancialDraft draft,string asset,long proceeds,AssetValueChoice choice,List<Account> accounts)
    {
        if(choice.Generation!=ValueState.Generation(db,owner,"Asset",asset))throw new ArgumentException("The asset value changed. Save and review this refund again.");
        if(string.IsNullOrWhiteSpace(draft.EvidenceId))throw new ArgumentException("Record the evidence and decision for this asset basis/recovery adjustment.");
        var book=AssetBookValue.Read(db,owner,asset,draft.Date!.Value);
        string Code(string code)=>accounts.Single(a=>a.Code==code && a.Active).Id;
        if(choice.Treatment==AssetValueChoice.Recover)
        {
            long reduction=Math.Min(proceeds,book.Carrying);
            var recovery=book.Reduce(reduction,asset);
            if(proceeds>reduction)recovery.Add(new(Code("4010"),0,proceeds-reduction,asset));
            return recovery;
        }
        if(choice.Treatment!=AssetValueChoice.Retire)throw new ArgumentException("Choose an asset basis recovery or return decision.");
        AssetRetirements.CheckLaterMovement(db,owner,asset,draft.Date.Value);
        var lines=book.Reduce(book.Cost,asset);
        if(book.Depreciation>0)lines.Add(new(Code("1590"),book.Depreciation,0,asset));
        if(proceeds>book.Carrying)lines.Add(new(Code("4010"),0,proceeds-book.Carrying,asset));
        if(proceeds<book.Carrying)lines.Add(new(Code("5060"),book.Carrying-proceeds,0,asset));
        return lines;
    }
}
