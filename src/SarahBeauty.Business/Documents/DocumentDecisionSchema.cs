namespace SarahBeauty.Business.Documents;

internal static class DocumentDecisionSchema
{
    internal const string Sql="""
        CREATE UNIQUE INDEX IF NOT EXISTS DocumentDecisionDocumentScope ON Documents(Id,BusinessId);
        CREATE UNIQUE INDEX IF NOT EXISTS DocumentDecisionVersionScope ON DocumentVersions(Id,DocumentId);
        CREATE UNIQUE INDEX IF NOT EXISTS DocumentDecisionEvidenceScope ON AttachmentVersions(Id,BusinessId);
        CREATE TABLE IF NOT EXISTS DocumentDecisions (
            Id TEXT PRIMARY KEY NOT NULL,
            BusinessId TEXT NOT NULL REFERENCES Business(Id),
            DocumentId TEXT NOT NULL,
            VersionId TEXT NOT NULL,
            RequestId TEXT NOT NULL,
            PayloadHash TEXT NOT NULL CHECK(length(PayloadHash)=64),
            SnapshotJson TEXT NOT NULL CHECK(json_valid(SnapshotJson)),
            SnapshotHash TEXT NOT NULL CHECK(length(SnapshotHash)=64),
            EvidenceVersionId TEXT,
            OwnerId TEXT NOT NULL,
            RecordedAtUtc TEXT NOT NULL,
            FOREIGN KEY(DocumentId,BusinessId) REFERENCES Documents(Id,BusinessId),
            FOREIGN KEY(VersionId,DocumentId) REFERENCES DocumentVersions(Id,DocumentId),
            FOREIGN KEY(OwnerId,BusinessId) REFERENCES Owners(Id,BusinessId),
            FOREIGN KEY(EvidenceVersionId,BusinessId) REFERENCES AttachmentVersions(Id,BusinessId)
        );
        CREATE UNIQUE INDEX IF NOT EXISTS DocumentDecisionVersionOnce ON DocumentDecisions(VersionId);
        CREATE UNIQUE INDEX IF NOT EXISTS DocumentDecisionRequestOnce ON DocumentDecisions(BusinessId,RequestId);
        CREATE TRIGGER IF NOT EXISTS DocumentDecisions_NoUpdate BEFORE UPDATE ON DocumentDecisions BEGIN SELECT RAISE(ABORT,'Document decisions are immutable'); END;
        CREATE TRIGGER IF NOT EXISTS DocumentDecisions_NoDelete BEFORE DELETE ON DocumentDecisions BEGIN SELECT RAISE(ABORT,'Document decisions are immutable'); END;
        """;
}
