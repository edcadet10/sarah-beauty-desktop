namespace SarahBeauty.Business.Documents;

internal static class DocumentSchema
{
    internal const string Sql = """
        CREATE TABLE DocumentCounters (
            BusinessId TEXT NOT NULL REFERENCES Business(Id), Type TEXT NOT NULL, NextNumber INTEGER NOT NULL,
            PRIMARY KEY(BusinessId,Type)
        );
        CREATE TABLE Documents (
            Id TEXT PRIMARY KEY NOT NULL, BusinessId TEXT NOT NULL REFERENCES Business(Id), Type TEXT NOT NULL,
            Number TEXT NOT NULL, BookingId TEXT REFERENCES Records(Id), CreatedBy TEXT NOT NULL REFERENCES Owners(Id),
            CreatedAtUtc TEXT NOT NULL, UNIQUE(BusinessId,Type,Number)
        );
        CREATE TABLE DocumentVersions (
            Id TEXT PRIMARY KEY NOT NULL, DocumentId TEXT NOT NULL REFERENCES Documents(Id), Version INTEGER NOT NULL,
            SourceDraftId TEXT NOT NULL UNIQUE REFERENCES Records(Id), IssueDate TEXT NOT NULL,
            DataJson TEXT NOT NULL CHECK(json_valid(DataJson)), SnapshotHash TEXT NOT NULL, Status TEXT NOT NULL,
            IssuedBy TEXT NOT NULL REFERENCES Owners(Id), IssuedAtUtc TEXT NOT NULL,
            AcceptedDate TEXT, AcceptanceEvidence TEXT, AcceptedBy TEXT REFERENCES Owners(Id),
            UNIQUE(DocumentId,Version)
        );
        CREATE TABLE DocumentArtifacts (
            VersionId TEXT PRIMARY KEY NOT NULL REFERENCES DocumentVersions(Id), Status TEXT NOT NULL DEFAULT 'Pending',
            RelativePath TEXT, Sha256 TEXT, Error TEXT
        );
        CREATE TRIGGER DocumentVersions_Immutable BEFORE UPDATE OF DocumentId,Version,SourceDraftId,IssueDate,
            DataJson,SnapshotHash,IssuedBy,IssuedAtUtc ON DocumentVersions
        BEGIN SELECT RAISE(ABORT,'Issued document contents are immutable.'); END;
        """;
}
