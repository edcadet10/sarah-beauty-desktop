using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

public sealed record AccountTotal(Account Account, long Opening, long Debits, long Credits)
{
    public long Ending => checked(Opening + Debits - Credits);
}
public sealed record ProfitAndLoss(long IncomeCents, long ExpenseCents)
{
    public long ResultCents => checked(IncomeCents - ExpenseCents);
}
public sealed record BalanceSheet(long AssetsCents, long LiabilitiesCents, long EquityCents)
{
    public bool Balances => AssetsCents == checked(LiabilitiesCents + EquityCents);
}

public sealed class LedgerReports(BusinessStore store)
{
    public List<AccountTotal> TrialBalance(OwnerSession owner, DateOnly from, DateOnly through) => store.Read(db =>
    {
        store.RequireSession(db, owner);
        if (from > through) throw new ArgumentException("The period start must be on or before its end.");
        return db.Read("""
            SELECT A.Id,A.Code,A.Name,A.Type,A.CashKind,A.Active,A.OpeningConfirmed,
                COALESCE(SUM(CASE WHEN M.Date<$from THEN J.DebitCents-J.CreditCents ELSE 0 END),0),
                COALESCE(SUM(CASE WHEN M.Date>=$from AND M.Date<=$through THEN J.DebitCents ELSE 0 END),0),
                COALESCE(SUM(CASE WHEN M.Date>=$from AND M.Date<=$through THEN J.CreditCents ELSE 0 END),0)
            FROM Accounts A LEFT JOIN JournalLines J ON J.AccountId=A.Id LEFT JOIN Movements M ON M.Id=J.MovementId
            WHERE A.BusinessId=$business GROUP BY A.Id ORDER BY A.Code;
            """, r => new AccountTotal(new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetInt32(5)==1,r.GetInt32(6)==1),
                r.GetInt64(7),r.GetInt64(8),r.GetInt64(9)), ("$business",owner.BusinessId),
            ("$from",from.ToString("yyyy-MM-dd")),("$through",through.ToString("yyyy-MM-dd")));
    });

    public ProfitAndLoss ProfitLoss(OwnerSession owner, DateOnly from, DateOnly through)
    {
        var rows = TrialBalance(owner, from, through);
        return new(rows.Where(r=>r.Account.Type=="Income").Sum(r=>r.Credits-r.Debits),
            rows.Where(r=>r.Account.Type=="Expense").Sum(r=>r.Debits-r.Credits));
    }

    public BalanceSheet BalanceSheet(OwnerSession owner, DateOnly through)
    {
        var rows = TrialBalance(owner, DateOnly.MinValue, through);
        return new(rows.Where(r=>r.Account.Type=="Asset").Sum(r=>r.Ending),
            -rows.Where(r=>r.Account.Type=="Liability").Sum(r=>r.Ending),
            -rows.Where(r=>r.Account.Type is "Equity" or "Income" or "Expense").Sum(r=>r.Ending));
    }
}
