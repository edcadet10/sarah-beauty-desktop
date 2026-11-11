namespace SarahBeauty.Business.Attachments;

internal static class AttachmentSchema
{
    internal const string Sql="""
        CREATE TABLE AttachmentVersions (
            Id TEXT NOT NULL PRIMARY KEY, BusinessId TEXT NOT NULL REFERENCES Business(Id), AttachmentId TEXT NOT NULL,
            Number INTEGER NOT NULL CHECK(Number>0), RequestId TEXT NOT NULL UNIQUE, PayloadHash TEXT NOT NULL,
            SnapshotJson TEXT NOT NULL CHECK(json_valid(SnapshotJson)), SnapshotHash TEXT NOT NULL,
            CreatedBy TEXT NOT NULL REFERENCES Owners(Id), AtUtc TEXT NOT NULL, UNIQUE(BusinessId,AttachmentId,Number)
        );
        CREATE INDEX AttachmentVersions_Business ON AttachmentVersions(BusinessId,AttachmentId,Number);
        CREATE TRIGGER AttachmentVersions_NoUpdate BEFORE UPDATE ON AttachmentVersions BEGIN SELECT RAISE(ABORT,'Attachment versions are immutable'); END;
        CREATE TRIGGER AttachmentVersions_NoDelete BEFORE DELETE ON AttachmentVersions BEGIN SELECT RAISE(ABORT,'Attachment versions are immutable'); END;
        """;
}
