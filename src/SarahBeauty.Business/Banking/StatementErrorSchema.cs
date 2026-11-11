namespace SarahBeauty.Business.Banking;

internal static class StatementErrorSchema
{
    internal const string Sql="""
        CREATE TABLE StatementErrors (
            Id TEXT NOT NULL PRIMARY KEY, ReconciliationId TEXT NOT NULL REFERENCES Reconciliations(Id),
            BankLineId TEXT NOT NULL REFERENCES BankLines(Id), Cents INTEGER NOT NULL CHECK(Cents>0),
            Explanation TEXT NOT NULL, EvidenceId TEXT NOT NULL, UNIQUE(ReconciliationId,BankLineId)
        );
        CREATE TABLE StatementErrorResolutions (
            ReconciliationId TEXT NOT NULL REFERENCES Reconciliations(Id), ErrorId TEXT NOT NULL REFERENCES StatementErrors(Id),
            BankLineId TEXT NOT NULL REFERENCES BankLines(Id), Cents INTEGER NOT NULL CHECK(Cents>0),
            Explanation TEXT NOT NULL, EvidenceId TEXT NOT NULL, PRIMARY KEY(ReconciliationId,ErrorId,BankLineId)
        );
        CREATE TRIGGER StatementErrors_NoUpdate BEFORE UPDATE ON StatementErrors BEGIN SELECT RAISE(ABORT,'Statement-error evidence is immutable'); END;
        CREATE TRIGGER StatementErrors_NoDelete BEFORE DELETE ON StatementErrors BEGIN SELECT RAISE(ABORT,'Statement-error evidence is immutable'); END;
        CREATE TRIGGER StatementErrorResolutions_NoUpdate BEFORE UPDATE ON StatementErrorResolutions BEGIN SELECT RAISE(ABORT,'Bank correction evidence is immutable'); END;
        CREATE TRIGGER StatementErrorResolutions_NoDelete BEFORE DELETE ON StatementErrorResolutions BEGIN SELECT RAISE(ABORT,'Bank correction evidence is immutable'); END;
        """;
}
