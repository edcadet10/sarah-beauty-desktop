using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Banking;

internal static class ReconciliationSourcesReader
{
    internal static ReconciliationSources Read(DbWork db,OwnerSession owner,string statementId)
    {
        var statement=StatementQueries.Imports(db,owner,statementId).SingleOrDefault()??throw new ArgumentException("Choose an imported statement from this business.");
        var account=ChartOfAccounts.List(db,owner).Single(a=>a.Id==statement.AccountId && a.CashKind!="None");
        var bank=StatementQueries.Lines(db,owner,account.Id).Where(b=>b.Date<=statement.Through).ToDictionary(b=>b.Id);
        var cash=BankMatchQueries.CashLines(db,owner,account.Id).Where(c=>c.Date<=statement.Through).ToList();
        var history=BankMatchQueries.History(db,owner);
        var bankPositions=BankMatching.BankPositions(bank.Values.ToList(),history,statement.Through);
        var cashPositions=BankMatching.CashPositions(cash,history,statement.Through);
        var cashById=cash.ToDictionary(c=>c.Id);
        return new(statement,(string)db.Scalar("SELECT DisplayName FROM Business WHERE Id=$id;",("$id",owner.BusinessId))!,
            (string)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!,account.Name,
            Convert.ToInt64(db.Scalar("SELECT Watermark FROM Business WHERE Id=$id;",("$id",owner.BusinessId))),DateTimeOffset.UtcNow.ToString("O"),
            ConfirmedOpening(db,owner,account.Id,statement.Through),Cents(cash.Select(c=>c.SignedCents)),
            bankPositions.Select(p=>new ReconciliationLine(p.Id,bank[p.Id].Date,p.SignedCents,p.RemainingCents,bank[p.Id].Description,"Bank transaction")).ToArray(),
            cashPositions.Select(p=>new ReconciliationLine(p.Id,cashById[p.Id].Date,cashById[p.Id].SignedCents,p.RemainingCents,cashById[p.Id].Description,cashById[p.Id].Kind)).ToArray(),Corrections(db,owner,account.Id,statement.Through));
    }

    private static bool ConfirmedOpening(DbWork db,OwnerSession owner,string account,DateOnly through)=>db.Scalar("""
        SELECT O.Id FROM OpeningBalances O WHERE O.BusinessId=$business AND O.AccountId=$account AND O.DimensionKind='Account' AND O.Date<=$date
        AND (O.RevokedAt IS NULL OR (O.ReversalMovementId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Movements R WHERE R.Id=O.ReversalMovementId AND R.Date<=$date))) LIMIT 1;
        """,("$business",owner.BusinessId),("$account",account),("$date",through.ToString("yyyy-MM-dd"))) is not null;

    private static PostedBookCorrection[] Corrections(DbWork db,OwnerSession owner,string account,DateOnly through)=>db.Read("""
        SELECT M.Id,M.Date,SUM(J.DebitCents-J.CreditCents),M.Memo,json_extract(M.DataJson,'$.EvidenceId') FROM Movements M
        JOIN JournalLines J ON J.MovementId=M.Id WHERE M.BusinessId=$business AND J.AccountId=$account AND M.Date<=$date
        AND COALESCE(json_extract(M.DataJson,'$.EvidenceId'),'')<>'' AND NOT EXISTS(SELECT 1 FROM Movements R WHERE R.ReversesId=M.Id AND R.Date<=$date)
        GROUP BY M.Id ORDER BY M.Date,M.Id;
        """,r=>new PostedBookCorrection(r.GetString(0),DateOnly.Parse(r.GetString(1)),r.GetInt64(2),r.GetString(3),r.GetString(4)),
        ("$business",owner.BusinessId),("$account",account),("$date",through.ToString("yyyy-MM-dd"))).ToArray();

    internal static long Cents(IEnumerable<long> values)
    {
        try{return checked((long)values.Aggregate(System.Numerics.BigInteger.Zero,(sum,c)=>sum+c));}
        catch(OverflowException){throw new ArgumentException("Reconciliation amounts exceed the supported cent range.");}
    }

    internal static long Difference(long left,long right)
    {
        try{return checked((long)((System.Numerics.BigInteger)left-right));}
        catch(OverflowException){throw new ArgumentException("Reconciliation differences exceed the supported cent range.");}
    }
}
