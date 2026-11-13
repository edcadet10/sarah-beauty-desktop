using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

internal static class ClientDraftDiscardWrites
{
    internal static void Save(DbWork db,OwnerSession owner,ClientDraftDiscard value)
    {
        var discarded=ClientDraftSnapshots.Discarded(ClientDraftSnapshots.Read(value.Kind,value.DraftSnapshotJson));string kind=ClientDraftSnapshots.RecordKind(value.Kind);
        int changed=db.Execute("UPDATE Records SET Revision=$next,DataJson=$json,UpdatedBy=$owner,UpdatedAtUtc=$at WHERE Id=$id AND BusinessId=$business AND Kind=$kind AND Revision=$previous;",("$next",discarded.Revision),("$json",ClientDraftSnapshots.Json(discarded)),("$owner",owner.OwnerId),("$at",value.RecordedAtUtc),("$id",value.DraftId),("$business",owner.BusinessId),("$kind",kind),("$previous",value.DraftRevision));
        if(changed!=1)throw new InvalidOperationException("The draft changed before its discard could be recorded. Reload its current state.");
        string json=JsonSerializer.Serialize(value);db.Execute("""
            INSERT INTO ClientDraftDiscards(Id,BusinessId,DraftId,Kind,RecordKind,DraftRevision,RequestId,PayloadHash,SnapshotJson,SnapshotHash,OwnerId,RecordedAtUtc)
            VALUES($id,$business,$draft,$kind,$record,$revision,$request,$payload,$json,$hash,$owner,$at);
            """,("$id",value.Id),("$business",value.BusinessId),("$draft",value.DraftId),("$kind",value.Kind.ToString()),("$record",kind),("$revision",value.DraftRevision),("$request",value.RequestId),("$payload",ClientDraftDiscardCodec.Payload(ClientDraftDiscardCodec.Request(value))),("$json",json),("$hash",ClientDraftDiscardCodec.Hash(json)),("$owner",value.OwnerId),("$at",value.RecordedAtUtc));
    }
}
