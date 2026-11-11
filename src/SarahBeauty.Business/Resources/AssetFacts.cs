using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Resources;

public sealed class AssetFacts(BusinessStore store)
{
    internal Action<string>? FailurePoint {get;set;}
    public bool RequiresReview(OwnerSession owner,string id)=>store.Read(db=>
    {
        store.RequireSession(db,owner);var asset=new RecordStore<Asset>(store,"Asset").Get(db,owner,id);
        return asset.FactHistory.Length>0 || HasBookHistory(db,id);
    });
    internal static bool HasBookHistory(DbWork db,string id)=>db.Scalar("SELECT Id FROM JournalLines WHERE AssetId=$id LIMIT 1;",("$id",id)) is not null;

    public Asset Review(OwnerSession owner,AssetFactDraft draft,int expectedRevision,string requestId,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);draft=draft with{Reason=draft.Reason.Trim(),EvidenceId=draft.EvidenceId.Trim()};
        string request=Guid.TryParse(requestId,out var id)?id.ToString():throw new ArgumentException("Use a permanent request ID for this reviewed asset fact decision.");
        if(draft.CostCents<0 || draft.TaxBasisCents<0 || draft.Reason.Length==0 || draft.EvidenceId.Length==0)
            throw new ArgumentException("Keep unknown facts blank and record a nonnegative value, actual review reason, and evidence.");
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);var records=new RecordStore<Asset>(store,"Asset");var current=records.Get(db,owner,draft.AssetId);
            var prior=current.FactHistory.SingleOrDefault(h=>h.RequestId==request);
            if(prior is not null)
            {
                if(prior.CostCents!=draft.CostCents || prior.TaxBasisCents!=draft.TaxBasisCents || prior.Reason!=draft.Reason || prior.EvidenceId!=draft.EvidenceId)
                    throw new ArgumentException("This request already belongs to a different reviewed asset fact decision.");
                return current;
            }
            if(current.Revision!=expectedRevision)throw new ArgumentException("The asset changed. Reload its current facts before reviewing them.");
            var review=new AssetFactReview(request,current.CostCents,current.TaxBasisCents,draft.CostCents,draft.TaxBasisCents,draft.Reason,draft.EvidenceId,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));
            var saved=records.Save(db,owner,current with{CostCents=draft.CostCents,TaxBasisCents=draft.TaxBasisCents,FactHistory=[..current.FactHistory,review]});
            FailurePoint?.Invoke("asset-facts");BusinessStore.Audit(db,owner,"Review current registered asset cost and tax basis",current.Id);FailurePoint?.Invoke("audit");return saved;
        });
    }
}
