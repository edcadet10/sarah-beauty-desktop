namespace SarahBeauty.Business.Finance;

internal static class OpeningSchema
{
    internal const string Sql="""
        CREATE TABLE OpeningBalances (
            Id TEXT PRIMARY KEY NOT NULL, BusinessId TEXT NOT NULL REFERENCES Business(Id),
            AccountId TEXT NOT NULL REFERENCES Accounts(Id), DimensionKind TEXT NOT NULL, DimensionId TEXT NOT NULL,
            Date TEXT NOT NULL, Cents INTEGER NOT NULL CHECK(Cents>=0), Credit INTEGER NOT NULL CHECK(Credit IN(0,1)),
            MovementId TEXT UNIQUE REFERENCES Movements(Id), Reason TEXT NOT NULL,Evidence TEXT NOT NULL,
            OwnerId TEXT NOT NULL REFERENCES Owners(Id),AtUtc TEXT NOT NULL,
            RevokedAt TEXT,RevokedBy TEXT REFERENCES Owners(Id),RevocationReason TEXT,ReversalMovementId TEXT REFERENCES Movements(Id)
        );
        CREATE UNIQUE INDEX OpeningBalances_Active ON OpeningBalances(BusinessId,AccountId,DimensionKind,DimensionId) WHERE RevokedAt IS NULL;
        CREATE TRIGGER OpeningBalances_Immutable BEFORE UPDATE OF Id,BusinessId,AccountId,DimensionKind,DimensionId,Date,Cents,Credit,
            MovementId,Reason,Evidence,OwnerId,AtUtc ON OpeningBalances BEGIN SELECT RAISE(ABORT,'Opening evidence is immutable.'); END;
        CREATE TRIGGER OpeningBalances_NoDelete BEFORE DELETE ON OpeningBalances BEGIN SELECT RAISE(ABORT,'Opening evidence cannot be deleted.'); END;
        """;
}
