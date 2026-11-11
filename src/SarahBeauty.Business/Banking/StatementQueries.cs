using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Banking;

internal static class StatementQueries
{
    internal static List<StatementImport> Imports(DbWork db,OwnerSession owner,string? id=null)=>db.Read("""
        SELECT S.Id,S.AccountId,S.PeriodStart,S.PeriodEnd,S.OpeningCents,S.ClosingCents,COALESCE(S.ContentHash,''),S.ApprovedBy,S.AtUtc,
            (SELECT COUNT(*) FROM StatementRows R WHERE R.StatementId=S.Id) FROM StatementImports S
        WHERE S.BusinessId=$business AND ($id IS NULL OR S.Id=$id) ORDER BY S.PeriodEnd,S.Id;
        """,r=>new StatementImport(r.GetString(0),r.GetString(1),DateOnly.Parse(r.GetString(2)),DateOnly.Parse(r.GetString(3)),r.GetInt64(4),r.GetInt64(5),r.GetString(6),r.GetString(7),r.GetString(8),r.GetInt32(9)),
        ("$business",owner.BusinessId),("$id",id));

    internal static List<BankLine> Lines(DbWork db,OwnerSession owner,string account)=>db.Read("""
        SELECT Id,AccountId,Date,SignedCents,Description,Reference,ReliableReference FROM BankLines WHERE BusinessId=$business AND AccountId=$account ORDER BY Date,Id;
        """,r=>new BankLine(r.GetString(0),r.GetString(1),DateOnly.Parse(r.GetString(2)),r.GetInt64(3),r.GetString(4),r.GetString(5),r.GetInt32(6)==1),
        ("$business",owner.BusinessId),("$account",account));

    internal static List<StatementImportLine> SourceRows(DbWork db,OwnerSession owner,string statement)=>db.Read("""
        SELECT R.SourceRow,B.Id,B.AccountId,B.Date,B.SignedCents,B.Description,B.Reference,B.ReliableReference,R.SourceJson,R.Reason
        FROM StatementRows R JOIN BankLines B ON B.Id=R.BankLineId JOIN StatementImports S ON S.Id=R.StatementId
        WHERE S.Id=$id AND S.BusinessId=$business ORDER BY R.SourceRow;
        """,r=>new StatementImportLine(r.GetInt32(0),new(r.GetString(1),r.GetString(2),DateOnly.Parse(r.GetString(3)),r.GetInt64(4),r.GetString(5),r.GetString(6),r.GetInt32(7)==1),
            JsonSerializer.Deserialize<StatementRow>(r.GetString(8))!,r.GetString(9)),("$id",statement),("$business",owner.BusinessId));
}
