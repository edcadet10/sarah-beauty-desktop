using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

public sealed partial class DocumentDecisions
{
    public DocumentDecision RecordQuote(OwnerSession owner,DocumentDecisionDraft draft,string phrase,string requestId,CancellationToken cancellation=default)=>Record(owner,draft,phrase,requestId,DocumentKind.Quote,cancellation);
    private DocumentVersion RequireQuote(DbWork db,OwnerSession owner,DocumentDecisionDraft draft)
    {
        var source=DocumentVersions.Find(db,owner,id:draft.VersionId)??throw new ArgumentException("Choose an issued quote from this business.");
        var today=DateOnly.FromDateTime((timeProvider??TimeProvider.System).GetLocalNow().DateTime);
        if(source.Snapshot.Kind!=DocumentKind.Quote||source.Hash!=draft.DocumentSnapshotHash||source.Status!=draft.ExpectedStatus||draft.DecisionDate<source.Snapshot.IssueDate||draft.DecisionDate>today||
            source.Snapshot.Mode!=(string?)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';"))throw new ArgumentException("Review the current issued quote and a decision date from its issue date through today.");
        if(DocumentDecisionQueries.Read(db,owner.BusinessId,source.Id).Count>0)throw new ArgumentException("This quote version already has a retained decision.");
        var booking=new RecordStore<Booking>(store,"Booking").Get(db,owner,source.BookingId);
        if(booking.AcceptedQuoteVersionId==source.Id)throw new ArgumentException("Review the accepted booking agreement through its cancellation workflow.");
        return source;
    }
}
