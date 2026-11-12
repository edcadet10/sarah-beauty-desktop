using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

internal static class DocumentDecisionWrites
{
    internal static void Insert(DbWork db,DocumentDecision value,string payload,string json,string hash)=>db.Execute("""
        INSERT INTO DocumentDecisions(Id,BusinessId,DocumentId,VersionId,RequestId,PayloadHash,SnapshotJson,SnapshotHash,EvidenceVersionId,OwnerId,RecordedAtUtc)
        VALUES($id,$business,$document,$version,$request,$payload,$json,$hash,$evidence,$owner,$at);
        """,("$id",value.Id),("$business",value.BusinessId),("$document",value.DocumentId),("$version",value.VersionId),("$request",value.RequestId),("$payload",payload),("$json",json),("$hash",hash),("$evidence",value.Evidence?.VersionId),("$owner",value.OwnerId),("$at",value.RecordedAtUtc));
    internal static void ChangeStatus(DbWork db,DocumentDecision value)
    {
        if(db.Execute("UPDATE DocumentVersions SET Status=$status WHERE Id=$version AND DocumentId=$document AND Status=$prior AND SnapshotHash=$hash;",
            ("$status",value.ResultStatus),("$version",value.VersionId),("$document",value.DocumentId),("$prior",value.PriorStatus),("$hash",value.DocumentSnapshotHash))!=1)
            throw new InvalidOperationException("The issued document changed. Reload its current status before recording a decision.");
    }
}
