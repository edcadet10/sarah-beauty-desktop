namespace SarahBeauty.Business.Purchasing;

internal static class PurchasePaymentSchema
{
    internal const string Sql="""
        CREATE TABLE PurchaseSettlements (
            Id TEXT PRIMARY KEY NOT NULL,MovementId TEXT NOT NULL REFERENCES Movements(Id),
            PurchaseId TEXT NOT NULL REFERENCES Records(Id),LineId TEXT NOT NULL,Date TEXT NOT NULL,
            Cents INTEGER NOT NULL CHECK(Cents>0),BusinessCents INTEGER NOT NULL CHECK(BusinessCents>=0 AND BusinessCents<=Cents),
            ReversesId TEXT UNIQUE REFERENCES PurchaseSettlements(Id),UNIQUE(MovementId,LineId)
        );
        CREATE INDEX PurchaseSettlements_Bill ON PurchaseSettlements(PurchaseId,Date);
        CREATE TRIGGER PurchaseSettlements_NoUpdate BEFORE UPDATE ON PurchaseSettlements BEGIN SELECT RAISE(ABORT,'Supplier settlements are immutable.'); END;
        CREATE TRIGGER PurchaseSettlements_NoDelete BEFORE DELETE ON PurchaseSettlements BEGIN SELECT RAISE(ABORT,'Supplier settlements are immutable.'); END;
        """;
}
