using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

public sealed partial class DocumentDecisions
{
    internal Action? BeforeDecisionCommit {get;set;}
    public DocumentDecision RecordQuote(OwnerSession owner,DocumentDecisionDraft draft,string phrase,string requestId,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();draft=DocumentDecisionRules.Normalize(draft);string request=DocumentDecisionRules.Identity(requestId);
        if(DocumentDecisionRules.Kind(draft.Action)!=DocumentKind.Quote)throw new ArgumentException("Choose a quote decision for this operation.");
        store.Read(db=>{store.RequireSession(db,owner);return true;});new OwnerAccess(store).Confirm(owner,phrase);cancellation.ThrowIfCancellationRequested();
        return store.Transaction(db=>RecordQuote(db,owner,draft,request,cancellation));
    }
    private DocumentDecision RecordQuote(DbWork db,OwnerSession owner,DocumentDecisionDraft draft,string request,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);string payload=DocumentDecisionCodec.Payload(draft);
        var prior=DocumentDecisionQueries.Prior(db,owner.BusinessId,request,payload);if(prior is not null){cancellation.ThrowIfCancellationRequested();return prior;}
        var source=RequireQuote(db,owner,draft);FinancialEvidence.Validate(db,store,owner,draft.Evidence,verifyContents:true);
        var value=new DocumentDecision(1,Guid.NewGuid().ToString(),owner.BusinessId,source.DocumentId,source.Id,request,draft.Action,draft.DecisionDate,source.Status,DocumentDecisionRules.Result(draft.Action),source.Hash,draft.Reason,draft.Evidence,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));
        string json=JsonSerializer.Serialize(value);DocumentDecisionWrites.Insert(db,value,payload,json,DocumentDecisionCodec.Hash(json));
        DocumentDecisionWrites.ChangeStatus(db,value);BusinessStore.Audit(db,owner,"Record issued quote decision",value.Id);
        BeforeDecisionCommit?.Invoke();cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);
        return DocumentDecisionQueries.Read(db,owner.BusinessId,decision:value.Id).Single();
    }
    private DocumentVersion RequireQuote(DbWork db,OwnerSession owner,DocumentDecisionDraft draft)
    {
        var source=DocumentVersions.Find(db,owner,id:draft.VersionId)??throw new ArgumentException("Choose an issued quote from this business.");
        if(source.Snapshot.Kind!=DocumentKind.Quote||source.Hash!=draft.DocumentSnapshotHash||source.Status!=draft.ExpectedStatus||draft.DecisionDate<source.Snapshot.IssueDate||
            source.Snapshot.Mode!=(string?)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';"))throw new ArgumentException("The quote source, status, or decision date changed. Review its current issued version before continuing.");
        if(DocumentDecisionQueries.Read(db,owner.BusinessId,source.Id).Count>0)throw new ArgumentException("This quote version already has a retained decision.");
        var booking=new RecordStore<Booking>(store,"Booking").Get(db,owner,source.BookingId);
        if(booking.AcceptedQuoteVersionId==source.Id)throw new ArgumentException("Review the accepted booking agreement through its cancellation workflow.");
        return source;
    }
}
