using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

public sealed partial class ClientDraftDiscards
{
    internal Action? BeforeDiscardCommit {get;set;}
    public ClientDraftDiscard Discard(OwnerSession owner,ClientDraftDiscardRequest request,string phrase,string requestId,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();request=ClientDraftDiscardRules.Normalize(request);
        if(!Guid.TryParseExact(requestId,"D",out var id)||id==Guid.Empty)throw new ArgumentException("Use a permanent request ID for this draft discard.");
        store.Read(db=>{store.RequireSession(db,owner);return true;});new OwnerAccess(store).Confirm(owner,phrase);cancellation.ThrowIfCancellationRequested();
        return store.Transaction(db=>Discard(db,owner,request,id.ToString(),cancellation));
    }
    private ClientDraftDiscard Discard(DbWork db,OwnerSession owner,ClientDraftDiscardRequest request,string requestId,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);var history=ClientDraftDiscardQueries.Read(db,owner.BusinessId);var prior=history.SingleOrDefault(v=>v.RequestId==requestId);
        if(prior is not null)
        {
            if(ClientDraftDiscardCodec.Payload(ClientDraftDiscardCodec.Request(prior))!=ClientDraftDiscardCodec.Payload(request))throw new ArgumentException("This request already recorded a different draft discard. Review its history before continuing.");
            cancellation.ThrowIfCancellationRequested();return prior;
        }
        if(history.Any(v=>v.DraftId==request.DraftId))throw new ArgumentException("This draft was already discarded. Review its retained history.");
        string json=ClientDraftSnapshots.Current(db,owner.BusinessId,request.Kind,request.DraftId);var source=ClientDraftSnapshots.Read(request.Kind,json);RequireUnissued(db,owner.BusinessId,source);
        if(source.Revision!=request.ExpectedRevision||ClientDraftDiscardCodec.Hash(json)!=request.DraftSnapshotHash)throw new ArgumentException("The saved draft changed. Reload and review its current contents before discarding it.");
        var value=new ClientDraftDiscard(1,Guid.NewGuid().ToString(),owner.BusinessId,source.Id,request.Kind,source.Revision,requestId,BusinessModes.StoredName(store.Mode),json,request.DraftSnapshotHash,request.Reason,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));
        ClientDraftDiscardWrites.Save(db,owner,value);BusinessStore.Audit(db,owner,"Discard client draft",value.Id);BeforeDiscardCommit?.Invoke();cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);return ClientDraftDiscardQueries.Read(db,owner.BusinessId).Single(v=>v.Id==value.Id);
    }
}
