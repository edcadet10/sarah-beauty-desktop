using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

public sealed partial class DocumentDecisions
{
    public DocumentDecision VoidInvoice(OwnerSession owner,DocumentDecisionDraft draft,string phrase,string requestId,CancellationToken cancellation=default)=>Record(owner,draft,phrase,requestId,DocumentKind.Invoice,cancellation);
    private DocumentVersion RequireInvoice(DbWork db,OwnerSession owner,DocumentDecisionDraft draft)
    {
        var source=DocumentVersions.Find(db,owner,id:draft.VersionId)??throw new ArgumentException("Choose an issued invoice from this business.");
        var today=DateOnly.FromDateTime((timeProvider??TimeProvider.System).GetLocalNow().DateTime);
        if(source.Snapshot.Kind!=DocumentKind.Invoice||source.Hash!=draft.DocumentSnapshotHash||source.Status!=draft.ExpectedStatus||draft.DecisionDate<source.Snapshot.IssueDate||draft.DecisionDate>today||
            source.Snapshot.Mode!=(string?)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';"))throw new ArgumentException("Review the current issued invoice and a void date from its issue date through today.");
        PostingValidation.CheckClosedPeriod(db,owner,draft.DecisionDate);
        if(DocumentDecisionQueries.Read(db,owner.BusinessId,source.Id).Count>0)throw new ArgumentException("This invoice already has a retained decision.");
        if(db.Scalar("SELECT Id FROM PaymentApplications WHERE InvoiceVersionId=$id LIMIT 1;",("$id",source.Id)) is not null||
            db.Scalar("SELECT VersionId FROM CreditNoteHeaders WHERE InvoiceVersionId=$id LIMIT 1;",("$id",source.Id)) is not null)
            throw new ArgumentException("This invoice has payment or credit-note history. Review its linked adjustments instead of voiding it.");
        var booking=new RecordStore<Booking>(store,"Booking").Get(db,owner,source.BookingId);
        if(booking.Status=="Cancelled")throw new ArgumentException("Review this invoice through the booking's retained cancellation history.");
        return source;
    }
}
