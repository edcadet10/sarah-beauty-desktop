using System.Text.Json;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

public sealed partial class DocumentDecisions
{
    internal Action? BeforeDecisionCommit {get;set;}
    private DocumentDecision Record(OwnerSession owner,DocumentDecisionDraft draft,string phrase,string requestId,DocumentKind kind,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();draft=DocumentDecisionRules.Normalize(draft);string request=DocumentDecisionRules.Identity(requestId);
        if(DocumentDecisionRules.Kind(draft.Action)!=kind)throw new ArgumentException("Choose a decision for this document type.");
        store.Read(db=>{store.RequireSession(db,owner);return true;});new OwnerAccess(store).Confirm(owner,phrase);cancellation.ThrowIfCancellationRequested();
        return store.Transaction(db=>Record(db,owner,draft,request,kind,cancellation));
    }
    private DocumentDecision Record(DbWork db,OwnerSession owner,DocumentDecisionDraft draft,string request,DocumentKind kind,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);string payload=DocumentDecisionCodec.Payload(draft);
        var prior=DocumentDecisionQueries.Prior(db,owner.BusinessId,request,payload);if(prior is not null){cancellation.ThrowIfCancellationRequested();return prior;}
        var source=kind==DocumentKind.Quote?RequireQuote(db,owner,draft):RequireInvoice(db,owner,draft);
        FinancialEvidence.Validate(db,store,owner,draft.Evidence,verifyContents:true);
        var value=new DocumentDecision(1,Guid.NewGuid().ToString(),owner.BusinessId,source.DocumentId,source.Id,request,draft.Action,draft.DecisionDate,source.Status,DocumentDecisionRules.Result(draft.Action),source.Hash,draft.Reason,draft.Evidence,owner.OwnerId,(timeProvider??TimeProvider.System).GetUtcNow().ToUniversalTime().ToString("O"));
        Save(db,owner,value,payload,source,kind);
        BeforeDecisionCommit?.Invoke();cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);
        return DocumentDecisionQueries.Read(db,owner.BusinessId,decision:value.Id).Single();
    }
    private static void Save(DbWork db,OwnerSession owner,DocumentDecision value,string payload,DocumentVersion source,DocumentKind kind)
    {
        string json=JsonSerializer.Serialize(value);DocumentDecisionWrites.Insert(db,value,payload,json,DocumentDecisionCodec.Hash(json));DocumentDecisionWrites.ChangeStatus(db,value);
        if(kind==DocumentKind.Invoice)
        {
            ValueState.Advance(db,owner,"Invoice",source.Id);
            db.Execute("INSERT INTO BillingState (BookingId,Generation) VALUES ($id,1) ON CONFLICT(BookingId) DO UPDATE SET Generation=Generation+1;",("$id",source.BookingId));
        }
        BusinessStore.Audit(db,owner,kind==DocumentKind.Quote?"Record issued quote decision":"Void unused invoice",value.Id);
    }
}
