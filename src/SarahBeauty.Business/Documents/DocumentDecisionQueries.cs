using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

internal static class DocumentDecisionQueries
{
    internal static IReadOnlyList<DocumentDecision> Read(DbWork db,string business,string version="",string decision="")
    {
        var values=db.Read("""
            SELECT Id,DocumentId,VersionId,RequestId,PayloadHash,SnapshotJson,SnapshotHash,EvidenceVersionId,OwnerId,RecordedAtUtc
            FROM DocumentDecisions WHERE BusinessId=$business AND ($version='' OR VersionId=$version) AND ($decision='' OR Id=$decision)
            ORDER BY RecordedAtUtc,Id;
            """,r=>
            {
                var value=DocumentDecisionCodec.Read(r.GetString(5),r.GetString(6));
                if(value.Id!=r.GetString(0)||value.BusinessId!=business||value.DocumentId!=r.GetString(1)||value.VersionId!=r.GetString(2)||value.RequestId!=r.GetString(3)||
                    DocumentDecisionCodec.Payload(DocumentDecisionCodec.Draft(value))!=r.GetString(4)||value.Evidence?.VersionId!=(r.IsDBNull(7)?null:r.GetString(7))||value.OwnerId!=r.GetString(8)||value.RecordedAtUtc!=r.GetString(9))throw DocumentDecisionCodec.Invalid();
                return value;
            },("$business",business),("$version",version),("$decision",decision));
        foreach(var value in values)CheckSource(db,value);return values.AsReadOnly();
    }
    internal static DocumentDecision? Prior(DbWork db,string business,string request,string payload)
    {
        var prior=db.Read("SELECT Id,PayloadHash FROM DocumentDecisions WHERE BusinessId=$business AND RequestId=$request;",r=>(Id:r.GetString(0),Payload:r.GetString(1)),("$business",business),("$request",request)).SingleOrDefault();
        if(prior.Id is null)return null;
        if(prior.Payload!=payload)throw new ArgumentException("This request already recorded a different document decision. Review its history before submitting another.");
        return Read(db,business,decision:prior.Id).Single();
    }
    private static void CheckSource(DbWork db,DocumentDecision value)
    {
        var source=DocumentVersions.Find(db,value.BusinessId,id:value.VersionId)??throw DocumentDecisionCodec.Invalid();
        if(source.DocumentId!=value.DocumentId||source.Hash!=value.DocumentSnapshotHash||source.Status!=value.ResultStatus||source.Snapshot.Kind!=DocumentDecisionRules.Kind(value.Action)||value.DecisionDate<source.Snapshot.IssueDate||
            db.Scalar("SELECT Id FROM Owners WHERE Id=$owner AND BusinessId=$business;",("$owner",value.OwnerId),("$business",value.BusinessId)) is null)throw DocumentDecisionCodec.Invalid();
        if(value.Evidence is null)return;var file=AttachmentQueries.List(db,value.BusinessId,versionId:value.Evidence.VersionId).SingleOrDefault();
        if(file is null||ManagedEvidenceFile.From(file)!=value.Evidence)throw DocumentDecisionCodec.Invalid();
    }
}
