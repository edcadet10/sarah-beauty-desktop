using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class ApplicationWriter
{
    internal static List<PaymentApplication> Save(DbWork db,OwnerSession owner,PaymentApplicationDraft draft,List<PaymentApplication> rows,Action<string>? failure)
    {
        string id=Guid.NewGuid().ToString();
        string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(draft))));
        db.Execute("""
            INSERT INTO ApplicationGroups (Id,BusinessId,DraftId,SourceRevision,PayloadHash,ApprovedBy,AtUtc)
            VALUES ($id,$business,$draft,$revision,$hash,$owner,$at);
            """,("$id",id),("$business",owner.BusinessId),("$draft",draft.Id),("$revision",draft.Revision),("$hash",hash),
            ("$owner",owner.OwnerId),("$at",DateTimeOffset.UtcNow.ToString("O")));
        failure?.Invoke("application-group");
        var saved=rows.Select(r=>r with{GroupId=id}).ToList();
        foreach(var row in saved)
        {
            db.Execute("""
                INSERT INTO PaymentApplications (Id,GroupId,PaymentId,CreditId,InvoiceVersionId,MilestoneId,Cents,TaxCents,Date,ReversesId)
                VALUES ($id,$group,$payment,$credit,$invoice,$milestone,$cents,$tax,$date,$reverses);
                """,("$id",row.Id),("$group",id),("$payment",row.PaymentId),("$credit",row.CreditId),("$invoice",row.InvoiceVersionId),
                ("$milestone",row.MilestoneId),("$cents",row.Cents),("$tax",row.TaxCents),("$date",row.Date.ToString("yyyy-MM-dd")),("$reverses",row.ReversesId));
            failure?.Invoke("application");
        }
        return saved;
    }

    internal static PaymentApplication Original(DbWork db,OwnerSession owner,string id)=>List(db,owner).SingleOrDefault(a=>a.Id==id)
        ??throw new ArgumentException("Choose an application from this business.");

    internal static List<PaymentApplication> List(DbWork db,OwnerSession owner,string? groupId=null)=>db.Read("""
        SELECT A.Id,A.GroupId,A.PaymentId,A.CreditId,A.InvoiceVersionId,A.MilestoneId,A.Cents,A.TaxCents,A.Date,A.ReversesId
        FROM PaymentApplications A JOIN ApplicationGroups G ON G.Id=A.GroupId
        WHERE G.BusinessId=$business AND ($group IS NULL OR A.GroupId=$group) ORDER BY A.Date,A.rowid;
        """,r=>new PaymentApplication(r.GetString(0),r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),r.IsDBNull(3)?null:r.GetString(3),
            r.GetString(4),r.GetString(5),r.GetInt64(6),r.GetInt64(7),DateOnly.Parse(r.GetString(8)),r.IsDBNull(9)?null:r.GetString(9)),
        ("$business",owner.BusinessId),("$group",groupId));
}
