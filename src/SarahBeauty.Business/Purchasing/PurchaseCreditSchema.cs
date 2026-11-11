namespace SarahBeauty.Business.Purchasing;

internal static class PurchaseCreditSchema
{
    internal const string Sql="""
        CREATE TRIGGER ApprovedPurchases_NoUpdate BEFORE UPDATE ON Records
        WHEN OLD.Kind IN('Purchase','PurchaseCredit') AND json_extract(OLD.DataJson,'$.Status')='Approved'
        BEGIN SELECT RAISE(ABORT,'Approved supplier source records are immutable.'); END;
        CREATE TRIGGER ApprovedPurchases_NoDelete BEFORE DELETE ON Records
        WHEN OLD.Kind IN('Purchase','PurchaseCredit') AND json_extract(OLD.DataJson,'$.Status')='Approved'
        BEGIN SELECT RAISE(ABORT,'Approved supplier source records cannot be deleted.'); END;
        """;
}
