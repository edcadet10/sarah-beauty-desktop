using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

public sealed partial class ClientDraftDiscards(BusinessStore store)
{
    public IReadOnlyList<ClientDraftDiscard> History(OwnerSession owner,CancellationToken cancellation=default)=>store.ReadSnapshot(db=>
    {
        cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);var values=ClientDraftDiscardQueries.Read(db,owner.BusinessId);cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);return values;
    });
}
