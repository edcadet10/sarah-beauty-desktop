namespace SarahBeauty.Business.Finance;

internal static class BookPeriodSchema
{
    internal const string Sql="""
        CREATE TABLE BookPeriodCloses (
            Id TEXT NOT NULL PRIMARY KEY, BusinessId TEXT NOT NULL REFERENCES Business(Id), FromDate TEXT NOT NULL, ThroughDate TEXT NOT NULL,
            RequestId TEXT NOT NULL UNIQUE, PayloadHash TEXT NOT NULL, SnapshotJson TEXT NOT NULL CHECK(json_valid(SnapshotJson)), SnapshotHash TEXT NOT NULL,
            ApprovedBy TEXT NOT NULL REFERENCES Owners(Id), AtUtc TEXT NOT NULL
        );
        CREATE INDEX BookPeriodCloses_Dates ON BookPeriodCloses(BusinessId,ThroughDate);
        CREATE TABLE BookPeriodReopens (
            Id TEXT NOT NULL PRIMARY KEY, PeriodId TEXT NOT NULL UNIQUE REFERENCES BookPeriodCloses(Id), RequestId TEXT NOT NULL UNIQUE,
            Reason TEXT NOT NULL, OwnerId TEXT NOT NULL REFERENCES Owners(Id), AtUtc TEXT NOT NULL
        );
        CREATE TRIGGER BookPeriodCloses_NoUpdate BEFORE UPDATE ON BookPeriodCloses BEGIN SELECT RAISE(ABORT,'Closed report snapshots are immutable'); END;
        CREATE TRIGGER BookPeriodCloses_NoDelete BEFORE DELETE ON BookPeriodCloses BEGIN SELECT RAISE(ABORT,'Closed report snapshots are immutable'); END;
        CREATE TRIGGER BookPeriodReopens_NoUpdate BEFORE UPDATE ON BookPeriodReopens BEGIN SELECT RAISE(ABORT,'Period reopen history is immutable'); END;
        CREATE TRIGGER BookPeriodReopens_NoDelete BEFORE DELETE ON BookPeriodReopens BEGIN SELECT RAISE(ABORT,'Period reopen history is immutable'); END;
        """;
}
