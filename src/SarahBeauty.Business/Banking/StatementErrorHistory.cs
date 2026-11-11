using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Banking;

internal sealed record BankReservation(string BankLineId,long Cents,DateOnly Date);

internal static class StatementErrorHistory
{
    internal static CarriedStatementError[] Carried(DbWork db,OwnerSession owner,string account,DateOnly through)=>db.Read("""
        SELECT E.Id,E.BankLineId,B.Date,E.Cents*CASE WHEN B.SignedCents>0 THEN 1 ELSE -1 END,
            E.Cents-COALESCE((SELECT SUM(R.Cents) FROM StatementErrorResolutions R JOIN Reconciliations C ON C.Id=R.ReconciliationId
                WHERE R.ErrorId=E.Id AND C.AsOfDate<$date AND NOT EXISTS(SELECT 1 FROM ReconciliationReopens X WHERE X.ReconciliationId=C.Id)),0),
            E.Explanation,E.EvidenceId FROM StatementErrors E JOIN Reconciliations C ON C.Id=E.ReconciliationId JOIN BankLines B ON B.Id=E.BankLineId
        WHERE C.BusinessId=$business AND C.AccountId=$account AND C.AsOfDate<$date
        AND NOT EXISTS(SELECT 1 FROM ReconciliationReopens X WHERE X.ReconciliationId=C.Id) ORDER BY B.Date,E.Id;
        """,r=>new CarriedStatementError(r.GetString(0),r.GetString(1),DateOnly.Parse(r.GetString(2)),r.GetInt64(3),r.GetInt64(4),r.GetString(5),r.GetString(6)),
        ("$business",owner.BusinessId),("$account",account),("$date",through.ToString("yyyy-MM-dd"))).Where(e=>e.RemainingCents>0).ToArray();

    internal static List<BankReservation> Reservations(DbWork db,OwnerSession owner,string account)=>db.Read("""
        SELECT E.BankLineId,E.Cents,C.AsOfDate FROM StatementErrors E JOIN Reconciliations C ON C.Id=E.ReconciliationId
        WHERE C.BusinessId=$business AND C.AccountId=$account AND NOT EXISTS(SELECT 1 FROM ReconciliationReopens X WHERE X.ReconciliationId=C.Id)
        UNION ALL
        SELECT R.BankLineId,R.Cents,C.AsOfDate FROM StatementErrorResolutions R JOIN Reconciliations C ON C.Id=R.ReconciliationId
        WHERE C.BusinessId=$business AND C.AccountId=$account AND NOT EXISTS(SELECT 1 FROM ReconciliationReopens X WHERE X.ReconciliationId=C.Id);
        """,r=>new BankReservation(r.GetString(0),r.GetInt64(1),DateOnly.Parse(r.GetString(2))),("$business",owner.BusinessId),("$account",account));

    internal static void Save(DbWork db,OwnerSession owner,string reconciliation,ReconciliationDraft draft,Action<string>? failure)
    {
        foreach(var error in draft.StatementErrors)
        {
            if(db.Scalar("SELECT Id FROM StatementErrors WHERE Id=$id;",("$id",error.Id)) is not null)
                throw new ArgumentException("Use a new permanent error ID for this revised reconciliation; the original evidence is retained.");
            db.Execute("INSERT INTO StatementErrors (Id,ReconciliationId,BankLineId,Cents,Explanation,EvidenceId) VALUES ($id,$reconciliation,$bank,$cents,$reason,$evidence);",
                ("$id",error.Id),("$reconciliation",reconciliation),("$bank",error.BankLineId),("$cents",error.Cents),("$reason",error.Explanation),("$evidence",error.EvidenceId));
            failure?.Invoke("statement-error");
        }
        foreach(var correction in draft.BankCorrections)
        {
            db.Execute("INSERT INTO StatementErrorResolutions (ReconciliationId,ErrorId,BankLineId,Cents,Explanation,EvidenceId) VALUES ($reconciliation,$error,$bank,$cents,$reason,$evidence);",
                ("$reconciliation",reconciliation),("$error",correction.ErrorId),("$bank",correction.BankLineId),("$cents",correction.Cents),("$reason",correction.Explanation),("$evidence",correction.EvidenceId));
            failure?.Invoke("statement-error-resolution");
        }
        Advance(db,owner,draft);
    }

    internal static void Advance(DbWork db,OwnerSession owner,ReconciliationDraft draft)
    {
        foreach(string bank in draft.StatementErrors.Select(e=>e.BankLineId).Concat(draft.BankCorrections.Select(r=>r.BankLineId)).Distinct())
            ValueState.Advance(db,owner,"BankMatchBank",bank);
    }

    internal static void CheckProposed(DbWork db,OwnerSession owner,ReconciliationPreview preview)
    {
        if(preview.BankErrorClaims.Count==0)return;
        var history=BankMatchQueries.History(db,owner,activeOnly:true).ToLookup(m=>m.BankLineId);
        var bank=preview.Sources.Bank.ToDictionary(b=>b.Id);
        foreach(var claim in preview.BankErrorClaims)
        {
            var rows=history[claim.Key].ToList();
            long current=BankMatchQueries.Matched(rows,preview.Sources.Statement.Through);
            long largest=MaximumMatched(rows,preview.Sources.Statement.Through,current);
            if(largest-current>bank[claim.Key].RemainingCents-claim.Value)
                throw new ArgumentException("A later bank match reserves this statement-error amount. Undo that matching dependency before completing.");
        }
    }

    private static long MaximumMatched(List<BankMatch> rows,DateOnly through,long current)
    {
        System.Numerics.BigInteger running=current,maximum=current;
        foreach(var date in rows.Where(r=>r.Date>through).GroupBy(r=>r.Date).OrderBy(g=>g.Key))
        {
            foreach(var row in date)running+=row.ReversesId.Length==0?row.Cents:-row.Cents;
            maximum=System.Numerics.BigInteger.Max(maximum,running);
        }
        return checked((long)maximum);
    }
}
