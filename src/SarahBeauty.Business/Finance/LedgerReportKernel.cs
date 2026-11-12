using System.Globalization;
using System.Numerics;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class LedgerReportKernel
{
    internal static List<AccountTotal> TrialBalance(DbWork db,string businessId,DateOnly from,DateOnly through)
    {
        if(from>through)throw new ArgumentException("The period start must be on or before its end.");
        string end=through.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        var accounts=db.Read("""
            SELECT A.Id,A.Code,A.Name,A.Type,A.CashKind,A.Active,EXISTS(
                SELECT 1 FROM OpeningBalances O WHERE O.BusinessId=A.BusinessId AND O.AccountId=A.Id AND O.DimensionKind='Account' AND O.Date<=$through
                AND (O.RevokedAt IS NULL OR (O.ReversalMovementId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Movements R WHERE R.Id=O.ReversalMovementId AND R.Date<=$through))))
            FROM Accounts A WHERE A.BusinessId=$business ORDER BY A.Code,A.Id;
            """,r=>new Totals(new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetInt32(5)==1,r.GetInt32(6)==1)),("$business",businessId),("$through",end));
        var byId=accounts.ToDictionary(a=>a.Account.Id);
        string start=from.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        using var command=db.Command("SELECT J.AccountId,M.Date,J.DebitCents,J.CreditCents FROM JournalLines J JOIN Movements M ON M.Id=J.MovementId WHERE M.BusinessId=$business AND M.Date<=$through;",("$business",businessId),("$through",end));
        using var reader=command.ExecuteReader();
        while(reader.Read())
        {
            if(!byId.TryGetValue(reader.GetString(0),out var account))throw new InvalidDataException("A journal line does not belong to the report's business accounts.");
            BigInteger debit=reader.GetInt64(2),credit=reader.GetInt64(3);
            if(string.CompareOrdinal(reader.GetString(1),start)<0)account.Opening+=debit-credit;
            else{account.Debits+=debit;account.Credits+=credit;}
        }
        return accounts.Select(a=>new AccountTotal(a.Account,Cents(a.Opening),Cents(a.Debits),Cents(a.Credits))).ToList();
    }

    internal static ProfitAndLoss ProfitLoss(IEnumerable<AccountTotal> rows)=>new(
        Sum(rows.Where(r=>r.Account.Type=="Income").Select(r=>(BigInteger)r.Credits-r.Debits)),
        Sum(rows.Where(r=>r.Account.Type=="Expense").Select(r=>(BigInteger)r.Debits-r.Credits)));

    internal static BalanceSheet BalanceSheet(IEnumerable<AccountTotal> rows)=>new(
        Sum(rows.Where(r=>r.Account.Type=="Asset").Select(Ending)),
        Sum(rows.Where(r=>r.Account.Type=="Liability").Select(r=>-Ending(r))),
        Sum(rows.Where(r=>r.Account.Type is "Equity" or "Income" or "Expense").Select(r=>-Ending(r))));

    private static BigInteger Ending(AccountTotal row)=>(BigInteger)row.Opening+row.Debits-row.Credits;
    private static long Sum(IEnumerable<BigInteger> values)=>Cents(values.Aggregate(BigInteger.Zero,(sum,value)=>sum+value));
    private static long Cents(BigInteger value)
    {
        try{return checked((long)value);}
        catch(OverflowException){throw new ArgumentException("The report total exceeds the supported cent range.");}
    }
    private sealed class Totals(Account account)
    {
        internal Account Account {get;}=account;
        internal BigInteger Opening,Debits,Credits;
    }
}
