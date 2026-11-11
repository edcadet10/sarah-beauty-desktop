namespace SarahBeauty.Business.Banking;

internal static class BankMatchSchema
{
    internal const string Sql="""
        CREATE TABLE BankMatchGroups (
            Id TEXT PRIMARY KEY NOT NULL, BusinessId TEXT NOT NULL REFERENCES Business(Id), RequestId TEXT NOT NULL UNIQUE,
            PayloadHash TEXT NOT NULL, DataJson TEXT NOT NULL CHECK(json_valid(DataJson)), ApprovedBy TEXT NOT NULL REFERENCES Owners(Id), AtUtc TEXT NOT NULL
        );
        CREATE TABLE BankMatches (
            Id TEXT PRIMARY KEY NOT NULL, GroupId TEXT NOT NULL REFERENCES BankMatchGroups(Id), BankLineId TEXT NOT NULL REFERENCES BankLines(Id),
            JournalLineId TEXT NOT NULL REFERENCES JournalLines(Id), Cents INTEGER NOT NULL CHECK(Cents>0), Date TEXT NOT NULL,
            ReversesId TEXT REFERENCES BankMatches(Id)
        );
        CREATE INDEX BankMatches_Bank ON BankMatches(BankLineId,Date);
        CREATE INDEX BankMatches_Journal ON BankMatches(JournalLineId,Date);
        CREATE INDEX BankMatches_Reverse ON BankMatches(ReversesId,Date);
        CREATE TRIGGER BankMatchGroups_NoUpdate BEFORE UPDATE ON BankMatchGroups BEGIN SELECT RAISE(ABORT,'Match approvals are immutable'); END;
        CREATE TRIGGER BankMatchGroups_NoDelete BEFORE DELETE ON BankMatchGroups BEGIN SELECT RAISE(ABORT,'Match approvals are immutable'); END;
        CREATE TRIGGER BankMatches_NoUpdate BEFORE UPDATE ON BankMatches BEGIN SELECT RAISE(ABORT,'Match history is immutable'); END;
        CREATE TRIGGER BankMatches_NoDelete BEFORE DELETE ON BankMatches BEGIN SELECT RAISE(ABORT,'Match history is immutable'); END;
        """;
}
