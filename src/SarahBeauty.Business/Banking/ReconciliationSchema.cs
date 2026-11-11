namespace SarahBeauty.Business.Banking;

internal static class ReconciliationSchema
{
    internal const string Sql="""
        CREATE TABLE Reconciliations (
            Id TEXT PRIMARY KEY NOT NULL, BusinessId TEXT NOT NULL REFERENCES Business(Id), AccountId TEXT NOT NULL REFERENCES Accounts(Id),
            StatementId TEXT NOT NULL REFERENCES StatementImports(Id), AsOfDate TEXT NOT NULL, RequestId TEXT NOT NULL UNIQUE,
            PayloadHash TEXT NOT NULL, SnapshotJson TEXT NOT NULL CHECK(json_valid(SnapshotJson)), SnapshotHash TEXT NOT NULL,
            ReviewedWatermark INTEGER NOT NULL, ApprovedBy TEXT NOT NULL REFERENCES Owners(Id), AtUtc TEXT NOT NULL
        );
        CREATE INDEX Reconciliations_AccountDate ON Reconciliations(AccountId,AsOfDate);
        CREATE TABLE ReconciliationBankCoverage (
            ReconciliationId TEXT NOT NULL REFERENCES Reconciliations(Id), BankLineId TEXT NOT NULL REFERENCES BankLines(Id),
            PRIMARY KEY(ReconciliationId,BankLineId)
        );
        CREATE TABLE ReconciliationReopens (
            Id TEXT PRIMARY KEY NOT NULL, ReconciliationId TEXT NOT NULL UNIQUE REFERENCES Reconciliations(Id), RequestId TEXT NOT NULL UNIQUE,
            Reason TEXT NOT NULL, OwnerId TEXT NOT NULL REFERENCES Owners(Id), AtUtc TEXT NOT NULL
        );
        CREATE TRIGGER Reconciliations_NoUpdate BEFORE UPDATE ON Reconciliations BEGIN SELECT RAISE(ABORT,'Completed reconciliation snapshots are immutable'); END;
        CREATE TRIGGER Reconciliations_NoDelete BEFORE DELETE ON Reconciliations BEGIN SELECT RAISE(ABORT,'Completed reconciliation snapshots are immutable'); END;
        CREATE TRIGGER ReconciliationBankCoverage_NoUpdate BEFORE UPDATE ON ReconciliationBankCoverage BEGIN SELECT RAISE(ABORT,'Reconciled bank coverage is immutable'); END;
        CREATE TRIGGER ReconciliationBankCoverage_NoDelete BEFORE DELETE ON ReconciliationBankCoverage BEGIN SELECT RAISE(ABORT,'Reconciled bank coverage is immutable'); END;
        CREATE TRIGGER ReconciliationReopens_NoUpdate BEFORE UPDATE ON ReconciliationReopens BEGIN SELECT RAISE(ABORT,'Reopen history is immutable'); END;
        CREATE TRIGGER ReconciliationReopens_NoDelete BEFORE DELETE ON ReconciliationReopens BEGIN SELECT RAISE(ABORT,'Reopen history is immutable'); END;
        """;
}
