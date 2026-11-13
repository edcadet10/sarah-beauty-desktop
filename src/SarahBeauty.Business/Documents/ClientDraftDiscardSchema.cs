namespace SarahBeauty.Business.Documents;

internal static class ClientDraftDiscardSchema
{
    internal const string Sql="""
        CREATE UNIQUE INDEX IF NOT EXISTS ClientDraftRecordScope ON Records(Id,BusinessId,Kind);
        CREATE TABLE IF NOT EXISTS ClientDraftDiscards (
            Id TEXT PRIMARY KEY NOT NULL, BusinessId TEXT NOT NULL REFERENCES Business(Id), DraftId TEXT NOT NULL,
            Kind TEXT NOT NULL CHECK(Kind IN ('Quote','Invoice','Receipt','CreditNote')),
            RecordKind TEXT NOT NULL CHECK((Kind='Quote' AND RecordKind='DocumentDraft') OR (Kind='Invoice' AND RecordKind='InvoiceDraft') OR (Kind='Receipt' AND RecordKind='ReceiptDraft') OR (Kind='CreditNote' AND RecordKind='CreditNoteDraft')),
            DraftRevision INTEGER NOT NULL CHECK(DraftRevision>0 AND DraftRevision<2147483647), RequestId TEXT NOT NULL,
            PayloadHash TEXT NOT NULL CHECK(length(PayloadHash)=64), SnapshotJson TEXT NOT NULL CHECK(json_valid(SnapshotJson)), SnapshotHash TEXT NOT NULL CHECK(length(SnapshotHash)=64),
            OwnerId TEXT NOT NULL, RecordedAtUtc TEXT NOT NULL,
            FOREIGN KEY(DraftId,BusinessId,RecordKind) REFERENCES Records(Id,BusinessId,Kind),
            FOREIGN KEY(OwnerId,BusinessId) REFERENCES Owners(Id,BusinessId)
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ClientDraftDiscardOnce ON ClientDraftDiscards(DraftId);
        CREATE UNIQUE INDEX IF NOT EXISTS ClientDraftDiscardRequestOnce ON ClientDraftDiscards(BusinessId,RequestId);
        CREATE TRIGGER IF NOT EXISTS ClientDraftDiscards_NoUpdate BEFORE UPDATE ON ClientDraftDiscards BEGIN SELECT RAISE(ABORT,'Client draft discards are immutable'); END;
        CREATE TRIGGER IF NOT EXISTS ClientDraftDiscards_NoDelete BEFORE DELETE ON ClientDraftDiscards BEGIN SELECT RAISE(ABORT,'Client draft discards are immutable'); END;
        """;
}
