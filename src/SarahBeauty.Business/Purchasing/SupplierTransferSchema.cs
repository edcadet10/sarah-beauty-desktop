namespace SarahBeauty.Business.Purchasing;

internal static class SupplierTransferSchema
{
    internal const string Sql="""
        CREATE TABLE PurchaseSettlements_Next (
            Id TEXT PRIMARY KEY NOT NULL,MovementId TEXT NOT NULL REFERENCES Movements(Id),
            PurchaseId TEXT NOT NULL REFERENCES Records(Id),LineId TEXT NOT NULL,Date TEXT NOT NULL,
            Cents INTEGER NOT NULL CHECK(Cents>0),BusinessCents INTEGER NOT NULL CHECK(BusinessCents>=0 AND BusinessCents<=Cents),
            ReversesId TEXT UNIQUE REFERENCES PurchaseSettlements_Next(Id),Sign INTEGER NOT NULL DEFAULT 1 CHECK(Sign IN(-1,1)),
            RefundsId TEXT REFERENCES PurchaseSettlements_Next(Id),
            DrawCents INTEGER NOT NULL DEFAULT 0 CHECK(DrawCents>=0 AND DrawCents<=Cents-BusinessCents),
            ConsumesId TEXT REFERENCES PurchaseSettlements_Next(Id),UNIQUE(MovementId,PurchaseId,LineId)
        );
        INSERT INTO PurchaseSettlements_Next SELECT Id,MovementId,PurchaseId,LineId,Date,Cents,BusinessCents,ReversesId,Sign,RefundsId,DrawCents,ConsumesId FROM PurchaseSettlements;
        DROP TABLE PurchaseSettlements;
        ALTER TABLE PurchaseSettlements_Next RENAME TO PurchaseSettlements;
        CREATE INDEX PurchaseSettlements_Bill ON PurchaseSettlements(PurchaseId,Date);
        CREATE INDEX PurchaseSettlements_Refund ON PurchaseSettlements(RefundsId);
        CREATE INDEX PurchaseSettlements_Consumes ON PurchaseSettlements(ConsumesId);
        CREATE TRIGGER PurchaseSettlements_NoUpdate BEFORE UPDATE ON PurchaseSettlements BEGIN SELECT RAISE(ABORT,'Supplier settlements are immutable.'); END;
        CREATE TRIGGER PurchaseSettlements_NoDelete BEFORE DELETE ON PurchaseSettlements BEGIN SELECT RAISE(ABORT,'Supplier settlements are immutable.'); END;
        """;
}
