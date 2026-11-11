namespace SarahBeauty.Business.Banking;

internal static class StatementSchema
{
    internal const string Sql="""
        CREATE TABLE StatementImports (
            Id TEXT PRIMARY KEY NOT NULL, BusinessId TEXT NOT NULL REFERENCES Business(Id), AccountId TEXT NOT NULL REFERENCES Accounts(Id),
            PeriodStart TEXT NOT NULL, PeriodEnd TEXT NOT NULL, OpeningCents INTEGER NOT NULL, ClosingCents INTEGER NOT NULL,
            ContentHash TEXT, MappingJson TEXT NOT NULL CHECK(json_valid(MappingJson)), SourceHash TEXT NOT NULL, PayloadHash TEXT NOT NULL,
            RequestId TEXT NOT NULL UNIQUE, ApprovedBy TEXT NOT NULL REFERENCES Owners(Id), AtUtc TEXT NOT NULL
        );
        CREATE UNIQUE INDEX StatementImports_Content ON StatementImports(AccountId,ContentHash) WHERE ContentHash IS NOT NULL;
        CREATE TABLE BankLines (
            Id TEXT PRIMARY KEY NOT NULL, BusinessId TEXT NOT NULL REFERENCES Business(Id), AccountId TEXT NOT NULL REFERENCES Accounts(Id),
            Date TEXT NOT NULL, SignedCents INTEGER NOT NULL CHECK(SignedCents<>0), Description TEXT NOT NULL, Reference TEXT NOT NULL,
            ReliableReference INTEGER NOT NULL CHECK(ReliableReference IN(0,1))
        );
        CREATE INDEX BankLines_Similar ON BankLines(AccountId,Date,SignedCents);
        CREATE UNIQUE INDEX BankLines_Reference ON BankLines(AccountId,Reference) WHERE ReliableReference=1;
        CREATE TABLE StatementRows (
            StatementId TEXT NOT NULL REFERENCES StatementImports(Id), SourceRow INTEGER NOT NULL CHECK(SourceRow>0),
            BankLineId TEXT NOT NULL REFERENCES BankLines(Id), SourceJson TEXT NOT NULL CHECK(json_valid(SourceJson)), Reason TEXT NOT NULL,
            PRIMARY KEY(StatementId,SourceRow), UNIQUE(StatementId,BankLineId)
        );
        CREATE TRIGGER StatementImports_NoUpdate BEFORE UPDATE ON StatementImports BEGIN SELECT RAISE(ABORT,'Imported statements are immutable'); END;
        CREATE TRIGGER StatementImports_NoDelete BEFORE DELETE ON StatementImports BEGIN SELECT RAISE(ABORT,'Imported statements are immutable'); END;
        CREATE TRIGGER BankLines_NoUpdate BEFORE UPDATE ON BankLines BEGIN SELECT RAISE(ABORT,'Imported bank lines are immutable'); END;
        CREATE TRIGGER BankLines_NoDelete BEFORE DELETE ON BankLines BEGIN SELECT RAISE(ABORT,'Imported bank lines are immutable'); END;
        CREATE TRIGGER StatementRows_NoUpdate BEFORE UPDATE ON StatementRows BEGIN SELECT RAISE(ABORT,'Statement source rows are immutable'); END;
        CREATE TRIGGER StatementRows_NoDelete BEFORE DELETE ON StatementRows BEGIN SELECT RAISE(ABORT,'Statement source rows are immutable'); END;
        """;
}
