namespace SarahBeauty.Business.Finance;

internal static class FinanceSchema
{
    internal const string Sql = """
        CREATE TABLE Accounts (
            Id TEXT PRIMARY KEY NOT NULL, BusinessId TEXT NOT NULL REFERENCES Business(Id),
            Code TEXT NOT NULL, Name TEXT NOT NULL, Type TEXT NOT NULL, CashKind TEXT NOT NULL,
            Active INTEGER NOT NULL DEFAULT 1, OpeningConfirmed INTEGER NOT NULL DEFAULT 0,
            UNIQUE(BusinessId,Code)
        );
        CREATE TABLE BookSettings (
            BusinessId TEXT PRIMARY KEY NOT NULL REFERENCES Business(Id), ClosedThrough TEXT,
            OpeningConfirmed INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE PostingBatches (
            Id TEXT PRIMARY KEY NOT NULL, BusinessId TEXT NOT NULL REFERENCES Business(Id),
            DraftId TEXT NOT NULL UNIQUE REFERENCES Records(Id), SourceRevision INTEGER NOT NULL,
            PayloadHash TEXT NOT NULL, ApprovedBy TEXT NOT NULL REFERENCES Owners(Id), AtUtc TEXT NOT NULL
        );
        CREATE TABLE Movements (
            Id TEXT PRIMARY KEY NOT NULL REFERENCES PostingBatches(Id), BusinessId TEXT NOT NULL REFERENCES Business(Id),
            Kind TEXT NOT NULL, Date TEXT NOT NULL, Memo TEXT NOT NULL, GrossCents INTEGER NOT NULL CHECK(GrossCents>=0),
            TaxCents INTEGER NOT NULL CHECK(TaxCents>=0), CustomerId TEXT REFERENCES Customers(Id),
            OwnerId TEXT REFERENCES Owners(Id), BookingId TEXT REFERENCES Records(Id),
            LoanId TEXT REFERENCES Records(Id), AssetId TEXT REFERENCES Records(Id), SupplierId TEXT REFERENCES Records(Id),
            OriginalId TEXT REFERENCES Movements(Id), ReversesId TEXT UNIQUE REFERENCES Movements(Id),
            ReferenceNamespace TEXT, ExternalReference TEXT, DataJson TEXT NOT NULL CHECK(json_valid(DataJson)),
            UNIQUE(BusinessId,ReferenceNamespace,ExternalReference)
        );
        CREATE TABLE JournalLines (
            Id TEXT PRIMARY KEY NOT NULL, MovementId TEXT NOT NULL REFERENCES Movements(Id), AccountId TEXT NOT NULL REFERENCES Accounts(Id),
            DebitCents INTEGER NOT NULL CHECK(DebitCents>=0), CreditCents INTEGER NOT NULL CHECK(CreditCents>=0),
            CHECK((DebitCents>0 AND CreditCents=0) OR (CreditCents>0 AND DebitCents=0))
        );
        CREATE INDEX Movements_Date ON Movements(BusinessId,Date);
        CREATE INDEX JournalLines_Account ON JournalLines(AccountId,MovementId);
        """;
}
