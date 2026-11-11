namespace SarahBeauty.Business.Documents;

internal static class ReceiptSchema
{
    internal const string Sql="""
        CREATE TABLE ReceiptHeaders (
            VersionId TEXT PRIMARY KEY NOT NULL REFERENCES DocumentVersions(Id),
            PaymentId TEXT NOT NULL UNIQUE REFERENCES Movements(Id)
        );
        CREATE TRIGGER ReceiptHeaders_NoUpdate BEFORE UPDATE ON ReceiptHeaders BEGIN SELECT RAISE(ABORT,'Receipt links are immutable.'); END;
        CREATE TRIGGER ReceiptHeaders_NoDelete BEFORE DELETE ON ReceiptHeaders BEGIN SELECT RAISE(ABORT,'Receipt links are immutable.'); END;
        """;
}
