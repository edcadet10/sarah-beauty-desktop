using Microsoft.Data.Sqlite;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class DatabaseBackupTests
{
    [Fact]
    public void SnapshotIncludesCommittedWalDataWithoutItsSidecarFiles()
    {
        using var store = new TestStore();
        store.CreateLegacy();
        using var writer = store.Open();
        using var command = writer.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA wal_autocheckpoint = 0;
            INSERT INTO AppMetadata VALUES ('WalExample', 'Committed in WAL');
            """;
        command.ExecuteNonQuery();
        Assert.True(new FileInfo(store.DatabasePath + "-wal").Length > 0);

        string snapshot = DatabaseBackup.CreateVerifiedSnapshot(store.DatabasePath);

        Assert.Equal("Committed in WAL", TestStore.Scalar(snapshot,
            "SELECT Value FROM AppMetadata WHERE Key = 'WalExample';"));
        Assert.Equal("ok", TestStore.Scalar(snapshot, "PRAGMA integrity_check;"));
        Assert.Equal("delete", TestStore.Scalar(snapshot, "PRAGMA journal_mode;"));
        Assert.False(File.Exists(snapshot + "-wal"));
        Assert.Empty(Directory.GetFiles(store.BackupFolder, "*.partial"));
    }

    [Fact]
    public void ForeignKeyFailureLeavesNoCompletedOrPartialSnapshot()
    {
        using var store = new TestStore();
        store.Execute("""
            PRAGMA foreign_keys = OFF;
            CREATE TABLE Parent (Id INTEGER PRIMARY KEY);
            CREATE TABLE Child (ParentId INTEGER REFERENCES Parent(Id));
            INSERT INTO Child VALUES (42);
            """);
        string before = store.Hash();

        var error = Assert.Throws<InvalidOperationException>(() =>
            DatabaseBackup.CreateVerifiedSnapshot(store.DatabasePath));

        Assert.Contains("foreign-key", error.Message);
        Assert.Equal(before, store.Hash());
        Assert.Empty(Directory.GetFiles(store.BackupFolder));
    }

    [Fact]
    public void CorruptSourceNeverBecomesACompletedSnapshot()
    {
        using var store = new TestStore();
        File.WriteAllText(store.DatabasePath, "This is not a SQLite database.");
        string before = store.Hash();

        Assert.Throws<SqliteException>(() =>
            DatabaseBackup.CreateVerifiedSnapshot(store.DatabasePath));

        Assert.Equal(before, store.Hash());
        Assert.Empty(store.Snapshots());
        if (Directory.Exists(store.BackupFolder))
        {
            Assert.Empty(Directory.GetFiles(store.BackupFolder));
        }
    }

    [Fact]
    public void MissingSourceIsNotCreatedByTheBackup()
    {
        using var store = new TestStore();
        Assert.Throws<SqliteException>(() =>
            DatabaseBackup.CreateVerifiedSnapshot(store.DatabasePath));
        Assert.False(File.Exists(store.DatabasePath));
        Assert.False(Directory.Exists(store.BackupFolder));
    }
}
