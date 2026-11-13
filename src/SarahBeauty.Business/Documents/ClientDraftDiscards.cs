using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

public sealed partial class ClientDraftDiscards(BusinessStore store)
{
    public ClientDraftDiscardRequest Review(OwnerSession owner,DocumentKind kind,string draftId,string reason,CancellationToken cancellation=default)=>store.ReadSnapshot(db=>
    {
        cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);_=ClientDraftDiscardQueries.Read(db,owner.BusinessId);string json=ClientDraftSnapshots.Current(db,owner.BusinessId,kind,draftId);var source=ClientDraftSnapshots.Read(kind,json);RequireUnissued(db,owner.BusinessId,source);
        var request=ClientDraftDiscardRules.Normalize(new(kind,draftId,source.Revision,ClientDraftDiscardCodec.Hash(json),reason));cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);return request;
    });
    public IReadOnlyList<ClientDraftDiscard> History(OwnerSession owner,CancellationToken cancellation=default)=>store.ReadSnapshot(db=>
    {
        cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);var values=ClientDraftDiscardQueries.Read(db,owner.BusinessId);cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);return values;
    });
    private static void RequireUnissued(DbWork db,string business,BusinessRecord source)
    {
        if(ClientDraftSnapshots.Status(source)!="Draft"||ClientDraftSnapshots.IssuedVersion(source)!=""||DocumentVersions.Find(db,business,draftId:source.Id) is not null)throw new ArgumentException("Choose an unissued client draft. Issued documents keep their separate decision and correction history.");
    }
}
