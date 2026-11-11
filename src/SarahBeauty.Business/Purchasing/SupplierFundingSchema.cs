namespace SarahBeauty.Business.Purchasing;

internal static class SupplierFundingSchema
{
    internal const string Sql="""
        ALTER TABLE PurchaseSettlements ADD COLUMN DrawCents INTEGER NOT NULL DEFAULT 0 CHECK(DrawCents>=0 AND DrawCents<=Cents-BusinessCents);
        ALTER TABLE PurchaseSettlements ADD COLUMN ConsumesId TEXT REFERENCES PurchaseSettlements(Id);
        DROP TRIGGER PurchaseSettlements_NoUpdate;
        UPDATE PurchaseSettlements SET DrawCents=Cents-BusinessCents WHERE MovementId IN
            (SELECT Id FROM Movements WHERE json_extract(DataJson,'$.PurchaseFunding')='Business cash');
        CREATE TRIGGER PurchaseSettlements_NoUpdate BEFORE UPDATE ON PurchaseSettlements BEGIN SELECT RAISE(ABORT,'Supplier settlements are immutable.'); END;
        CREATE INDEX PurchaseSettlements_Consumes ON PurchaseSettlements(ConsumesId);
        """;
}
