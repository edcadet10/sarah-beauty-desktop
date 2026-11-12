namespace SarahBeauty.Business.Ownership;

internal static class OwnerInterestSchema
{
    internal const string Sql="""
        CREATE UNIQUE INDEX IF NOT EXISTS OwnerInterestOwnerScope ON Owners(Id,BusinessId);
        CREATE TABLE IF NOT EXISTS OwnerInterestVersions (
            Id TEXT PRIMARY KEY NOT NULL,
            BusinessId TEXT NOT NULL REFERENCES Business(Id),
            Revision INTEGER NOT NULL CHECK(Revision>0),
            EffectiveFrom TEXT NOT NULL CHECK(length(EffectiveFrom)=10),
            SupersedesId TEXT CHECK(SupersedesId IS NULL OR SupersedesId<>Id),
            RecordedByOwnerId TEXT NOT NULL,
            RecordedAtUtc TEXT NOT NULL,
            EvidenceVersionId TEXT REFERENCES AttachmentVersions(Id),
            SnapshotJson TEXT NOT NULL,
            SnapshotHash TEXT NOT NULL CHECK(length(SnapshotHash)=64),
            UNIQUE(Id,BusinessId), UNIQUE(BusinessId,Revision),
            FOREIGN KEY(RecordedByOwnerId,BusinessId) REFERENCES Owners(Id,BusinessId),
            FOREIGN KEY(SupersedesId,BusinessId) REFERENCES OwnerInterestVersions(Id,BusinessId)
        );
        CREATE UNIQUE INDEX IF NOT EXISTS OwnerInterestInitialDate ON OwnerInterestVersions(BusinessId,EffectiveFrom) WHERE SupersedesId IS NULL;
        CREATE UNIQUE INDEX IF NOT EXISTS OwnerInterestCorrection ON OwnerInterestVersions(BusinessId,SupersedesId) WHERE SupersedesId IS NOT NULL;
        CREATE TABLE IF NOT EXISTS OwnerInterestShares (
            VersionId TEXT NOT NULL,
            BusinessId TEXT NOT NULL,
            OwnerId TEXT NOT NULL,
            OwnerNameAtConfirmation TEXT NOT NULL,
            FractionText TEXT NOT NULL CHECK(length(FractionText) BETWEEN 1 AND 16),
            PRIMARY KEY(VersionId,OwnerId),
            FOREIGN KEY(VersionId,BusinessId) REFERENCES OwnerInterestVersions(Id,BusinessId),
            FOREIGN KEY(OwnerId,BusinessId) REFERENCES Owners(Id,BusinessId)
        );
        """;
}
