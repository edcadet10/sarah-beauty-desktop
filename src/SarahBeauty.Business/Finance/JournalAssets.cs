using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class JournalAssets
{
    internal static List<JournalLine> Assign(DbWork db,BusinessStore store,OwnerSession owner,FinancialDraft draft,List<JournalLine> lines,List<Account> accounts)
    {
        if(draft.AssetId.Length>0)_=new RecordStore<Asset>(store,"Asset").Get(db,owner,draft.AssetId);
        return lines.Select(line=>
        {
            var account=accounts.Single(a=>a.Id==line.AccountId);
            bool asset=account.Type=="Asset" && account.CashKind=="None";
            if(line.AssetId.Length>0 && !asset)throw new ArgumentException("An individual asset link belongs to a noncash asset journal line.");
            string id=line.AssetId.Length>0?line.AssetId:asset?draft.AssetId:"";
            if(id.Length>0)_=new RecordStore<Asset>(store,"Asset").Get(db,owner,id);
            return line with{AssetId=id};
        }).ToList();
    }

    internal static void AfterPosting(DbWork db,OwnerSession owner,List<JournalLine> lines)
    {
        foreach(string asset in lines.Where(l=>l.AssetId.Length>0).Select(l=>l.AssetId).Distinct())
        {
            BalanceGuards.ValidateAsset(db,owner,asset);
            ValueState.Advance(db,owner,"Asset",asset);
        }
    }
}
