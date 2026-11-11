namespace SarahBeauty.Business.Documents;

internal static class CreditNoteSchema
{
    internal const string Sql="""
        CREATE TABLE CreditNoteHeaders (
            VersionId TEXT PRIMARY KEY NOT NULL REFERENCES DocumentVersions(Id),
            InvoiceVersionId TEXT NOT NULL REFERENCES InvoiceHeaders(VersionId), Date TEXT NOT NULL
        );
        CREATE TABLE CreditNoteLines (
            NoteVersionId TEXT NOT NULL REFERENCES CreditNoteHeaders(VersionId), ChargeId TEXT NOT NULL,
            Quantity TEXT NOT NULL, NetCents INTEGER NOT NULL CHECK(NetCents>=0),
            TaxCents INTEGER NOT NULL CHECK(TaxCents>=0), DiscountCents INTEGER NOT NULL CHECK(DiscountCents>=0),
            PRIMARY KEY(NoteVersionId,ChargeId)
        );
        CREATE TABLE CreditNoteMilestones (
            NoteVersionId TEXT NOT NULL REFERENCES CreditNoteHeaders(VersionId), MilestoneId TEXT NOT NULL REFERENCES InvoiceMilestones(Id),
            Cents INTEGER NOT NULL CHECK(Cents>0),TaxCents INTEGER NOT NULL CHECK(TaxCents>=0 AND TaxCents<=Cents),
            PRIMARY KEY(NoteVersionId,MilestoneId)
        );
        CREATE TRIGGER CreditNoteHeaders_NoUpdate BEFORE UPDATE ON CreditNoteHeaders BEGIN SELECT RAISE(ABORT,'Credit notes are immutable.'); END;
        CREATE TRIGGER CreditNoteHeaders_NoDelete BEFORE DELETE ON CreditNoteHeaders BEGIN SELECT RAISE(ABORT,'Credit notes are immutable.'); END;
        CREATE TRIGGER CreditNoteLines_NoUpdate BEFORE UPDATE ON CreditNoteLines BEGIN SELECT RAISE(ABORT,'Credit note lines are immutable.'); END;
        CREATE TRIGGER CreditNoteLines_NoDelete BEFORE DELETE ON CreditNoteLines BEGIN SELECT RAISE(ABORT,'Credit note lines are immutable.'); END;
        CREATE TRIGGER CreditNoteMilestones_NoUpdate BEFORE UPDATE ON CreditNoteMilestones BEGIN SELECT RAISE(ABORT,'Credit note milestones are immutable.'); END;
        CREATE TRIGGER CreditNoteMilestones_NoDelete BEFORE DELETE ON CreditNoteMilestones BEGIN SELECT RAISE(ABORT,'Credit note milestones are immutable.'); END;
        """;
}
