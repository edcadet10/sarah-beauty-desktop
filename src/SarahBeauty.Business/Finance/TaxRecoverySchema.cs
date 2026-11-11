namespace SarahBeauty.Business.Finance;

internal static class TaxRecoverySchema
{
    internal const string Sql="""
        INSERT OR IGNORE INTO Accounts (Id,BusinessId,Code,Name,Type,CashKind)
        SELECT lower(hex(randomblob(16))),Id,'1250','Sales tax recoverable','Asset','None' FROM Business;
        INSERT OR IGNORE INTO Accounts (Id,BusinessId,Code,Name,Type,CashKind)
        SELECT lower(hex(randomblob(16))),Id,'5050','Nonrecoverable refunded tax','Expense','None' FROM Business;
        """;
}
