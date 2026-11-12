using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Banking;

internal static class ReconciliationQueries
{
    internal static List<CompletedReconciliation> List(DbWork db,OwnerSession owner,string? id=null)=>List(db,owner.BusinessId,id);
    internal static List<CompletedReconciliation> List(DbWork db,string businessId,string? id=null)=>db.Read("""
        SELECT C.Id,C.AccountId,C.StatementId,C.AsOfDate,C.ApprovedBy,C.AtUtc,C.SnapshotJson,C.SnapshotHash,
            EXISTS(SELECT 1 FROM ReconciliationReopens R WHERE R.ReconciliationId=C.Id) FROM Reconciliations C
        WHERE C.BusinessId=$business AND ($id IS NULL OR C.Id=$id) ORDER BY C.AsOfDate,C.AtUtc,C.Id;
        """,r=>
        {
            string json=r.GetString(6),hash=r.GetString(7);
            if(Hash(json)!=hash)throw new InvalidDataException("A saved reconciliation snapshot failed its content-hash check.");
            return new CompletedReconciliation(r.GetString(0),r.GetString(1),r.GetString(2),DateOnly.Parse(r.GetString(3)),r.GetString(4),r.GetString(5),
                JsonSerializer.Deserialize<ReconciliationPreview>(json)!,hash,r.GetInt32(8)==1);
        },("$business",businessId),("$id",id));

    internal static (string[] PriorIds,HashSet<string> BankIds) Coverage(DbWork db,OwnerSession owner,string account,DateOnly through)
    {
        var rows=db.Read("""
            SELECT C.Id,B.BankLineId FROM Reconciliations C LEFT JOIN ReconciliationBankCoverage B ON B.ReconciliationId=C.Id
            WHERE C.BusinessId=$business AND C.AccountId=$account AND C.AsOfDate<$date
            AND NOT EXISTS(SELECT 1 FROM ReconciliationReopens R WHERE R.ReconciliationId=C.Id);
            """,r=>(Id:r.GetString(0),Bank:r.IsDBNull(1)?null:r.GetString(1)),("$business",owner.BusinessId),("$account",account),("$date",through.ToString("yyyy-MM-dd")));
        return(rows.Select(r=>r.Id).Distinct().ToArray(),rows.Where(r=>r.Bank is not null).Select(r=>r.Bank!).ToHashSet());
    }

    internal static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
