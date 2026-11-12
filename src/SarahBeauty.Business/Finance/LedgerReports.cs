using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

public sealed record AccountTotal(Account Account, long Opening, long Debits, long Credits)
{
    public long Ending => checked((long)((System.Numerics.BigInteger)Opening + Debits - Credits));
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
    public List<AccountTotal> TrialBalance(OwnerSession owner, DateOnly from, DateOnly through) => store.ReadSnapshot(db =>
    {
        store.RequireSession(db, owner);
        return TrialBalance(db,owner,from,through);
    });

    internal static List<AccountTotal> TrialBalance(DbWork db,OwnerSession owner,DateOnly from,DateOnly through)=>LedgerReportKernel.TrialBalance(db,owner.BusinessId,from,through);

    public ProfitAndLoss ProfitLoss(OwnerSession owner, DateOnly from, DateOnly through)
    {
        var rows = TrialBalance(owner, from, through);
        return LedgerReportKernel.ProfitLoss(rows);
    }

    public BalanceSheet BalanceSheet(OwnerSession owner, DateOnly through)
    {
        var rows = TrialBalance(owner, DateOnly.MinValue, through);
        return LedgerReportKernel.BalanceSheet(rows);
    }
}
