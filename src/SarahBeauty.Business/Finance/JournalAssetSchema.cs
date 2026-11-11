namespace SarahBeauty.Business.Finance;

internal static class JournalAssetSchema
{
    internal const string Sql="""
        ALTER TABLE JournalLines ADD COLUMN AssetId TEXT REFERENCES Records(Id);
        UPDATE JournalLines SET AssetId=(SELECT M.AssetId FROM Movements M WHERE M.Id=JournalLines.MovementId)
        WHERE AccountId IN(SELECT Id FROM Accounts WHERE Type='Asset' AND CashKind='None');
        CREATE INDEX JournalLines_Asset ON JournalLines(AssetId,MovementId);
        """;
}
