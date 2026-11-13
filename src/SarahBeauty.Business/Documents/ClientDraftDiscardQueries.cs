using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

internal static class ClientDraftDiscardQueries
{
    internal static IReadOnlyList<ClientDraftDiscard> Read(DbWork db,string business)
    {
        var values=db.Read("SELECT Id,DraftId,Kind,RecordKind,DraftRevision,RequestId,PayloadHash,SnapshotJson,SnapshotHash,OwnerId,RecordedAtUtc FROM ClientDraftDiscards WHERE BusinessId=$business ORDER BY RecordedAtUtc,Id;",r=>
        {
            var v=ClientDraftDiscardCodec.Read(r.GetString(7),r.GetString(8));
            if(v.Id!=r.GetString(0)||v.BusinessId!=business||v.DraftId!=r.GetString(1)||v.Kind.ToString()!=r.GetString(2)||ClientDraftSnapshots.RecordKind(v.Kind)!=r.GetString(3)||v.DraftRevision!=r.GetInt32(4)||v.RequestId!=r.GetString(5)||ClientDraftDiscardCodec.Payload(ClientDraftDiscardCodec.Request(v))!=r.GetString(6)||v.OwnerId!=r.GetString(9)||v.RecordedAtUtc!=r.GetString(10))throw ClientDraftDiscardCodec.Invalid();return v;
        },("$business",business));
        string? mode=db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';") as string;var drafts=new HashSet<string>(StringComparer.Ordinal);var requests=new HashSet<string>(StringComparer.Ordinal);
        foreach(var v in values)
        {
            if(v.Mode!=mode||!drafts.Add(v.DraftId)||!requests.Add(v.RequestId)||db.Scalar("SELECT Id FROM Owners WHERE Id=$id AND BusinessId=$business;",("$id",v.OwnerId),("$business",business)) is null)throw ClientDraftDiscardCodec.Invalid();
            string expected=ClientDraftSnapshots.Json(ClientDraftSnapshots.Discarded(ClientDraftSnapshots.Read(v.Kind,v.DraftSnapshotJson)));
            if(ClientDraftSnapshots.Current(db,business,v.Kind,v.DraftId)!=expected||DocumentVersions.Find(db,business,draftId:v.DraftId) is not null)throw ClientDraftDiscardCodec.Invalid();
        }
        foreach(string id in db.Read("SELECT Id FROM Records WHERE BusinessId=$business AND Kind IN ('DocumentDraft','InvoiceDraft','ReceiptDraft','CreditNoteDraft') AND json_extract(DataJson,'$.Status')='Discarded';",r=>r.GetString(0),("$business",business)))if(!drafts.Contains(id))throw ClientDraftDiscardCodec.Invalid();
        return values.AsReadOnly();
    }
}
