using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Banking;

internal static class BankMatchQueries
{
    internal static List<BankMatch> History(DbWork db,OwnerSession owner)=>db.Read("""
        SELECT M.Id,M.GroupId,M.BankLineId,M.JournalLineId,M.Cents,M.Date,COALESCE(M.ReversesId,'') FROM BankMatches M
        JOIN BankMatchGroups G ON G.Id=M.GroupId WHERE G.BusinessId=$business ORDER BY M.Date,M.Id;
        """,r=>new BankMatch(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetInt64(4),DateOnly.Parse(r.GetString(5)),r.GetString(6)),("$business",owner.BusinessId));

    internal static List<CashMatchLine> CashLines(DbWork db,OwnerSession owner,string? account=null)=>db.Read("""
        SELECT J.Id,M.Id,J.AccountId,M.Date,J.DebitCents-J.CreditCents,M.Memo,R.Date FROM JournalLines J JOIN Movements M ON M.Id=J.MovementId
        JOIN Accounts A ON A.Id=J.AccountId LEFT JOIN Movements R ON R.ReversesId=M.Id
        WHERE M.BusinessId=$business AND A.CashKind<>'None' AND ($account IS NULL OR J.AccountId=$account) ORDER BY M.Date,J.Id;
        """,r=>new CashMatchLine(r.GetString(0),r.GetString(1),r.GetString(2),DateOnly.Parse(r.GetString(3)),r.GetInt64(4),r.GetString(5),r.IsDBNull(6)?null:DateOnly.Parse(r.GetString(6))),
        ("$business",owner.BusinessId),("$account",account));

    internal static long Matched(IEnumerable<BankMatch> rows,DateOnly date)=>checked((long)rows.Where(r=>r.Date<=date)
        .Aggregate(System.Numerics.BigInteger.Zero,(sum,r)=>sum+(r.ReversesId.Length==0?r.Cents:-r.Cents)));
}
