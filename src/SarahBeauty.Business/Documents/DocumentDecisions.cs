using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

public sealed partial class DocumentDecisions(BusinessStore store)
{
    public IReadOnlyList<DocumentDecision> History(OwnerSession owner,string versionId="")=>store.ReadSnapshot(db=>{store.RequireSession(db,owner);return DocumentDecisionQueries.Read(db,owner.BusinessId,versionId);});
}
