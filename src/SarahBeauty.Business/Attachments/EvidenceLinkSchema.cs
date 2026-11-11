namespace SarahBeauty.Business.Attachments;

internal static class EvidenceLinkSchema
{
    internal const string Sql="""
        CREATE TABLE AttachmentLinks (
            Id TEXT NOT NULL PRIMARY KEY, BusinessId TEXT NOT NULL REFERENCES Business(Id), VersionId TEXT NOT NULL REFERENCES AttachmentVersions(Id),
            TargetKind INTEGER NOT NULL, TargetId TEXT NOT NULL, RequestId TEXT NOT NULL UNIQUE, PayloadHash TEXT NOT NULL,
            SnapshotJson TEXT NOT NULL CHECK(json_valid(SnapshotJson)), SnapshotHash TEXT NOT NULL, OwnerId TEXT NOT NULL REFERENCES Owners(Id), AtUtc TEXT NOT NULL
        );
        CREATE INDEX AttachmentLinks_Target ON AttachmentLinks(BusinessId,TargetKind,TargetId);
        CREATE INDEX AttachmentLinks_Version ON AttachmentLinks(VersionId);
        CREATE TABLE AttachmentLinkReleases (
            Id TEXT NOT NULL PRIMARY KEY, BusinessId TEXT NOT NULL REFERENCES Business(Id), LinkId TEXT NOT NULL UNIQUE REFERENCES AttachmentLinks(Id),
            RequestId TEXT NOT NULL UNIQUE, PayloadHash TEXT NOT NULL, SnapshotJson TEXT NOT NULL CHECK(json_valid(SnapshotJson)),
            SnapshotHash TEXT NOT NULL, OwnerId TEXT NOT NULL REFERENCES Owners(Id), AtUtc TEXT NOT NULL
        );
        CREATE TRIGGER AttachmentLinks_NoUpdate BEFORE UPDATE ON AttachmentLinks BEGIN SELECT RAISE(ABORT,'Evidence links are immutable'); END;
        CREATE TRIGGER AttachmentLinks_NoDelete BEFORE DELETE ON AttachmentLinks BEGIN SELECT RAISE(ABORT,'Evidence links are immutable'); END;
        CREATE TRIGGER AttachmentLinkReleases_NoUpdate BEFORE UPDATE ON AttachmentLinkReleases BEGIN SELECT RAISE(ABORT,'Evidence link releases are immutable'); END;
        CREATE TRIGGER AttachmentLinkReleases_NoDelete BEFORE DELETE ON AttachmentLinkReleases BEGIN SELECT RAISE(ABORT,'Evidence link releases are immutable'); END;
        """;
}
