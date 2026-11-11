namespace SarahBeauty.Business.Banking;

internal static class StatementWithdrawalSchema
{
    internal const string Sql="""
        CREATE TABLE StatementWithdrawals (
            Id TEXT NOT NULL PRIMARY KEY, StatementId TEXT NOT NULL UNIQUE REFERENCES StatementImports(Id), RequestId TEXT NOT NULL UNIQUE,
            Reason TEXT NOT NULL, EvidenceId TEXT NOT NULL, OwnerId TEXT NOT NULL REFERENCES Owners(Id), AtUtc TEXT NOT NULL
        );
        CREATE TRIGGER StatementWithdrawals_NoUpdate BEFORE UPDATE ON StatementWithdrawals BEGIN SELECT RAISE(ABORT,'Statement withdrawal history is immutable'); END;
        CREATE TRIGGER StatementWithdrawals_NoDelete BEFORE DELETE ON StatementWithdrawals BEGIN SELECT RAISE(ABORT,'Statement withdrawal history is immutable'); END;
        DROP INDEX StatementImports_Content;
        CREATE INDEX StatementImports_Content ON StatementImports(AccountId,ContentHash) WHERE ContentHash IS NOT NULL;
        CREATE TRIGGER StatementImports_ActiveContent BEFORE INSERT ON StatementImports
        WHEN NEW.ContentHash IS NOT NULL AND EXISTS(SELECT 1 FROM StatementImports S WHERE S.AccountId=NEW.AccountId AND S.ContentHash=NEW.ContentHash
            AND NOT EXISTS(SELECT 1 FROM StatementWithdrawals W WHERE W.StatementId=S.Id))
        BEGIN SELECT RAISE(ABORT,'This content already has an active import'); END;
        DROP INDEX BankLines_Reference;
        CREATE INDEX BankLines_Reference ON BankLines(AccountId,Reference) WHERE ReliableReference=1;
        CREATE TRIGGER BankLines_ActiveReference BEFORE INSERT ON BankLines
        WHEN NEW.ReliableReference=1 AND EXISTS(SELECT 1 FROM BankLines B WHERE B.AccountId=NEW.AccountId AND B.Reference=NEW.Reference AND B.ReliableReference=1
            AND EXISTS(SELECT 1 FROM StatementRows R WHERE R.BankLineId=B.Id AND NOT EXISTS(SELECT 1 FROM StatementWithdrawals W WHERE W.StatementId=R.StatementId)))
        BEGIN SELECT RAISE(ABORT,'This reference already identifies an active bank transaction'); END;
        """;
}
