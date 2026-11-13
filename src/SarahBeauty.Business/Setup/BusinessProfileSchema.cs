namespace SarahBeauty.Business.Setup;

internal static class BusinessProfileSchema
{
    internal const string Sql="""
        CREATE TABLE IF NOT EXISTS BusinessProfileVersions (
            Id TEXT PRIMARY KEY NOT NULL, BusinessId TEXT NOT NULL REFERENCES Business(Id), Revision INTEGER NOT NULL CHECK(Revision>0),
            RequestId TEXT NOT NULL, SnapshotJson TEXT NOT NULL CHECK(json_valid(SnapshotJson)), SnapshotHash TEXT NOT NULL CHECK(length(SnapshotHash)=64),
            PayloadHash TEXT NOT NULL CHECK(length(PayloadHash)=64), RecordedByOwnerId TEXT NOT NULL, RecordedAtUtc TEXT NOT NULL,
            FOREIGN KEY(RecordedByOwnerId,BusinessId) REFERENCES Owners(Id,BusinessId)
        );
        CREATE UNIQUE INDEX IF NOT EXISTS BusinessProfileRevisionOnce ON BusinessProfileVersions(BusinessId,Revision);
        CREATE UNIQUE INDEX IF NOT EXISTS BusinessProfileRequestOnce ON BusinessProfileVersions(BusinessId,RequestId);
        CREATE TRIGGER IF NOT EXISTS BusinessProfileVersions_NoUpdate BEFORE UPDATE ON BusinessProfileVersions BEGIN SELECT RAISE(ABORT,'Business profiles are immutable'); END;
        CREATE TRIGGER IF NOT EXISTS BusinessProfileVersions_NoDelete BEFORE DELETE ON BusinessProfileVersions BEGIN SELECT RAISE(ABORT,'Business profiles are immutable'); END;
        """;
}
