using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Documents;

internal static class DocumentVersions
{
    internal static DocumentVersion Save(DbWork db,OwnerSession owner,DocumentDraft draft,ClientSnapshot snapshot)
    {
        string id=Guid.NewGuid().ToString();
        string json=JsonSerializer.Serialize(snapshot);
        string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)));
        db.Execute("UPDATE DocumentVersions SET Status='Superseded' WHERE DocumentId=$document AND Status='Issued';",
            ("$document",draft.DocumentId));
        db.Execute("""
            INSERT INTO DocumentVersions (Id,DocumentId,Version,SourceDraftId,IssueDate,DataJson,SnapshotHash,Status,IssuedBy,IssuedAtUtc)
            VALUES ($id,$document,$version,$draft,$date,$json,$hash,'Issued',$owner,$at);
            INSERT INTO DocumentArtifacts (VersionId) VALUES ($id);
            """,("$id",id),("$document",draft.DocumentId),("$version",snapshot.Version),("$draft",draft.Id),
            ("$date",snapshot.IssueDate.ToStorageDate()),("$json",json),("$hash",hash),
            ("$owner",owner.OwnerId),("$at",DateTimeOffset.UtcNow.ToString("O")));
        return new(id,draft.DocumentId,draft.BookingId,snapshot,hash,"Issued",null,null);
    }

    internal static DocumentVersion? Find(DbWork db,OwnerSession owner,string? id=null,string? draftId=null)=>Find(db,owner.BusinessId,id,draftId);
    internal static DocumentVersion? Find(DbWork db,string businessId,string? id=null,string? draftId=null)=>db.Read("""
        SELECT V.Id,V.DocumentId,V.DataJson,V.SnapshotHash,V.Status,V.AcceptedDate,V.AcceptanceEvidence,D.BookingId
        FROM DocumentVersions V JOIN Documents D ON D.Id=V.DocumentId
        WHERE D.BusinessId=$business AND (($id IS NOT NULL AND V.Id=$id) OR ($draft IS NOT NULL AND V.SourceDraftId=$draft));
        """,Read,("$business",businessId),("$id",id),("$draft",draftId)).SingleOrDefault();

    internal static List<DocumentVersion> List(DbWork db,OwnerSession owner)=>List(db,owner.BusinessId);
    internal static List<DocumentVersion> List(DbWork db,string businessId)=>db.Read("""
        SELECT V.Id,V.DocumentId,V.DataJson,V.SnapshotHash,V.Status,V.AcceptedDate,V.AcceptanceEvidence,D.BookingId
        FROM DocumentVersions V JOIN Documents D ON D.Id=V.DocumentId
        WHERE D.BusinessId=$business ORDER BY D.CreatedAtUtc,V.Version;
        """,Read,("$business",businessId));

    private static DocumentVersion Read(Microsoft.Data.Sqlite.SqliteDataReader r)
    {
        string json=r.GetString(2),hash=r.GetString(3);
        if(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)))!=hash)
            throw new InvalidOperationException("The document snapshot failed its integrity check.");
        return new(r.GetString(0),r.GetString(1),r.IsDBNull(7)?"":r.GetString(7),JsonSerializer.Deserialize<ClientSnapshot>(json)!,hash,r.GetString(4),
            r.IsDBNull(5)?null:StoredDates.Parse(r.GetString(5)),r.IsDBNull(6)?null:r.GetString(6));
    }

    internal static string AllocateNumber(DbWork db,OwnerSession owner,DocumentKind kind)
    {
        string type=kind.ToString();
        db.Execute("INSERT OR IGNORE INTO DocumentCounters (BusinessId,Type,NextNumber) VALUES ($business,$type,1);",("$business",owner.BusinessId),("$type",type));
        long next=Convert.ToInt64(db.Scalar("SELECT NextNumber FROM DocumentCounters WHERE BusinessId=$business AND Type=$type;",("$business",owner.BusinessId),("$type",type)));
        db.Execute("UPDATE DocumentCounters SET NextNumber=NextNumber+1 WHERE BusinessId=$business AND Type=$type;",("$business",owner.BusinessId),("$type",type));
        string prefix=kind switch{DocumentKind.Quote=>"Q",DocumentKind.Invoice=>"I",DocumentKind.Receipt=>"R",_=>"C"};
        return $"{prefix}-{next:D6}";
    }
}
