using Microsoft.Data.Sqlite;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class LocalDatabaseTests
{
    [Fact]
    public void FreshStoreKeepsItsIdentityAndTestModeAfterReopening()
    {
        using var store = new TestStore();
        var first = LocalDatabase.InitializeTestDatabaseAt(store.DatabasePath);
        var second = LocalDatabase.InitializeTestDatabaseAt(store.DatabasePath);

        Assert.Equal(first, second);
        Assert.Equal(2, first.SchemaVersion);
        Assert.True(Guid.TryParse(first.StoreId, out _));
        Assert.Equal("TEST", TestStore.Scalar(store.DatabasePath,
            "SELECT Value FROM AppMetadata WHERE Key = 'StoreMode';"));
        Assert.Empty(store.Snapshots());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpgradeRetainsAnOpenableVersionZeroSnapshotAndExistingMetadata(bool useWal)
    {
        using var store = new TestStore();
        store.CreateLegacy();
        using var writer = useWal ? store.Open() : null;
        if (writer is not null)
        {
            using var command = writer.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode = WAL;
                UPDATE AppMetadata SET Value = 'Keep this example' WHERE Key = 'FictionalNote';
                """;
            command.ExecuteNonQuery();
        }

        var result = LocalDatabase.InitializeTestDatabaseAt(store.DatabasePath);
        string snapshot = Assert.Single(store.Snapshots());
        Assert.Equal(TestStore.StoreId, result.StoreId);
        Assert.Equal(2, result.SchemaVersion);
        Assert.Equal(0L, TestStore.Scalar(snapshot, "PRAGMA user_version;"));
        Assert.Equal(TestStore.StoreId, TestStore.Scalar(snapshot,
            "SELECT Value FROM AppMetadata WHERE Key = 'StoreId';"));
        Assert.Equal("Keep this example", TestStore.Scalar(snapshot,
            "SELECT Value FROM AppMetadata WHERE Key = 'FictionalNote';"));
        Assert.Equal(0L, TestStore.Scalar(snapshot,
            "SELECT COUNT(*) FROM AppMetadata WHERE Key = 'StoreMode';"));
        Assert.Equal("Keep this example", TestStore.Scalar(store.DatabasePath,
            "SELECT Value FROM AppMetadata WHERE Key = 'FictionalNote';"));

        Assert.Equal(result, LocalDatabase.InitializeTestDatabaseAt(store.DatabasePath));
        Assert.Single(store.Snapshots());
    }

    [Fact]
    public void ExistingTestModeCanBeUpgradedWithoutReplacingTheIdentity()
    {
        using var store = new TestStore();
        store.CreateLegacy();
        store.Execute("INSERT INTO AppMetadata VALUES ('StoreMode', 'TEST');");

        var result = LocalDatabase.InitializeTestDatabaseAt(store.DatabasePath);

        Assert.Equal(TestStore.StoreId, result.StoreId);
        Assert.Equal(2, result.SchemaVersion);
        Assert.Single(store.Snapshots());
    }

    [Fact]
    public async Task ConcurrentInitializationsPerformOnlyOneUpgrade()
    {
        using var store = new TestStore();
        store.CreateLegacy();

        var results = await Task.WhenAll(
            Task.Run(() => LocalDatabase.InitializeTestDatabaseAt(store.DatabasePath)),
            Task.Run(() => LocalDatabase.InitializeTestDatabaseAt(store.DatabasePath)));

        Assert.Equal(results[0], results[1]);
        Assert.Equal(TestStore.StoreId, results[0].StoreId);
        Assert.Single(store.Snapshots());
    }

    [Fact]
    public void MissingStoreIdentityIsRejectedWithoutChanges()
    {
        using var store = new TestStore();
        store.CreateLegacy();
        store.Execute("DELETE FROM AppMetadata WHERE Key = 'StoreId';");
        AssertRejectedWithoutChanges(store, "store ID");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void UnsupportedVersionLeavesTheFileUnchanged(int version)
    {
        using var store = new TestStore();
        store.CreateLegacy(version);
        AssertRejectedWithoutChanges(store, "unsupported");
    }

    [Fact]
    public void NextSchemaVersionIsRejectedWithoutChanges()
    {
        using var store = new TestStore();
        store.CreateLegacy(SarahBeauty.Business.Storage.BusinessSchema.Version + 1);
        AssertRejectedWithoutChanges(store, "unsupported");
    }

    [Theory]
    [InlineData(0, "LIVE")]
    [InlineData(1, "LIVE")]
    [InlineData(0, "test")]
    [InlineData(1, "UNKNOWN")]
    public void WrongStoreModeLeavesTheFileUnchanged(int version, string mode)
    {
        using var store = new TestStore();
        store.CreateLegacy(version);
        using (var connection = store.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO AppMetadata VALUES ('StoreMode', $mode);";
            command.Parameters.AddWithValue("$mode", mode);
            command.ExecuteNonQuery();
        }

        AssertRejectedWithoutChanges(store, "requires a TEST store");
    }

    [Fact]
    public void VersionOneWithMissingModeIsRejectedWithoutChanges()
    {
        using var store = new TestStore();
        store.CreateLegacy(1);
        AssertRejectedWithoutChanges(store, "requires a TEST store");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void BinaryStoreModeIsRejectedWithoutChanges(int version)
    {
        using var store = new TestStore();
        store.CreateLegacy(version);
        store.Execute("INSERT INTO AppMetadata VALUES ('StoreMode', X'4C495645');");
        AssertRejectedWithoutChanges(store, "must be text");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void InvalidStoreIdentityIsRejectedWithoutChanges(string value)
    {
        using var store = new TestStore();
        store.CreateLegacy();
        using (var connection = store.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE AppMetadata SET Value = $value WHERE Key = 'StoreId';";
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
        }

        AssertRejectedWithoutChanges(store, "store ID");
    }

    [Theory]
    [InlineData("CREATE TABLE Other (Value TEXT);")]
    [InlineData("CREATE TABLE AppMetadata (Key TEXT, Value TEXT);")]
    [InlineData("CREATE VIEW AppMetadata AS SELECT 'StoreId' AS Key, 'example' AS Value;")]
    [InlineData("CREATE TABLE AppMetadata (Key TEXT NOT NULL PRIMARY KEY, Value TEXT NOT NULL, Extra TEXT);")]
    public void UnexpectedSchemaIsRejectedWithoutChanges(string schema)
    {
        using var store = new TestStore();
        store.Execute(schema);
        AssertRejectedWithoutChanges(store, "expected metadata schema");
    }

    [Fact]
    public void BackupFailurePreventsTheUpgrade()
    {
        using var store = new TestStore();
        store.CreateLegacy();
        File.WriteAllText(store.BackupFolder, "Block creation of the backup folder.");
        string before = store.Hash();

        Assert.ThrowsAny<IOException>(() =>
            LocalDatabase.InitializeTestDatabaseAt(store.DatabasePath));

        Assert.Equal(before, store.Hash());
        Assert.Equal(0L, TestStore.Scalar(store.DatabasePath, "PRAGMA user_version;"));
    }

    [Fact]
    public void FailedMigrationRollsBackAndKeepsTheVerifiedSnapshot()
    {
        using var store = new TestStore();
        store.Execute("""
            CREATE TABLE AppMetadata (
                Key TEXT NOT NULL PRIMARY KEY CHECK (Key <> 'StoreMode'),
                Value TEXT NOT NULL
            );
            INSERT INTO AppMetadata VALUES ('StoreId', '9a737a42-8baa-4e78-9449-641c2b1fa64a');
            """);
        string before = store.Hash();

        Assert.Throws<SqliteException>(() =>
            LocalDatabase.InitializeTestDatabaseAt(store.DatabasePath));

        Assert.Equal(before, store.Hash());
        string snapshot = Assert.Single(store.Snapshots());
        Assert.Equal(0L, TestStore.Scalar(snapshot, "PRAGMA user_version;"));
        Assert.Equal(TestStore.StoreId, TestStore.Scalar(snapshot,
            "SELECT Value FROM AppMetadata WHERE Key = 'StoreId';"));
    }

    private static void AssertRejectedWithoutChanges(TestStore store, string message)
    {
        string before = store.Hash();
        var error = Assert.Throws<InvalidOperationException>(() =>
            LocalDatabase.InitializeTestDatabaseAt(store.DatabasePath));
        Assert.Contains(message, error.Message);
        Assert.Equal(before, store.Hash());
        Assert.Empty(store.Snapshots());
    }
}
