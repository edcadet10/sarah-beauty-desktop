namespace SarahBeauty.Business.Purchasing;

internal static class PurchaseRefundSchema
{
    internal const string Sql="""
        ALTER TABLE PurchaseSettlements ADD COLUMN Sign INTEGER NOT NULL DEFAULT 1 CHECK(Sign IN(-1,1));
        ALTER TABLE PurchaseSettlements ADD COLUMN RefundsId TEXT REFERENCES PurchaseSettlements(Id);
        DROP TRIGGER PurchaseSettlements_NoUpdate;
        UPDATE PurchaseSettlements SET Sign=-1 WHERE ReversesId IS NOT NULL;
        CREATE TRIGGER PurchaseSettlements_NoUpdate BEFORE UPDATE ON PurchaseSettlements BEGIN SELECT RAISE(ABORT,'Supplier settlements are immutable.'); END;
        CREATE INDEX PurchaseSettlements_Refund ON PurchaseSettlements(RefundsId);
        """;
}
