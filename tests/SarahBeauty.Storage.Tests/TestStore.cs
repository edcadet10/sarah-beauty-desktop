using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace SarahBeauty.Storage.Tests;

internal sealed class TestStore : IDisposable
{
    internal const string StoreId = "9a737a42-8baa-4e78-9449-641c2b1fa64a";
    internal string Folder { get; } = Path.Combine(
        Path.GetTempPath(), "SarahBeautyStorageTests", Guid.NewGuid().ToString("N"));
    internal string DatabasePath => Path.Combine(Folder, "sarahbeauty.db");
    internal string BackupFolder => Path.Combine(Folder, "Backups");

    internal TestStore() => Directory.CreateDirectory(Folder);

    internal SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Pooling = false,
            DefaultTimeout = 1
        }.ToString());
        connection.Open();
        return connection;
    }

    internal void CreateLegacy(int version = 0)
    {
        Execute("""
            CREATE TABLE AppMetadata (Key TEXT NOT NULL PRIMARY KEY, Value TEXT NOT NULL);
            INSERT INTO AppMetadata VALUES ('StoreId', '9a737a42-8baa-4e78-9449-641c2b1fa64a');
            INSERT INTO AppMetadata VALUES ('FictionalNote', 'Keep this example');
            """);
        Execute($"PRAGMA user_version = {version};");
    }

    internal void Execute(string sql)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    internal void PrepareBankingVersion(int version)
    {
        if(version is <18 or >28)throw new ArgumentOutOfRangeException(nameof(version));
        var tables=new List<string>{"AttachmentVersions"};
        if(version<28)tables.AddRange(["TripPaymentReleases","TripPaymentLinks"]);
        if(version<27)tables.Add("CancellationDecisions");
        if(version<26)tables.AddRange(["BookPeriodReopens","BookPeriodCloses"]);
        if(version<25)tables.Add("StatementWithdrawals");
        if(version<24)tables.AddRange(["StatementErrorResolutions","StatementErrors"]);
        if(version<23)tables.AddRange(["ReconciliationBankCoverage","ReconciliationReopens","Reconciliations"]);
        if(version<21)tables.AddRange(["BankMatches","BankMatchGroups"]);
        if(version<20)tables.AddRange(["StatementRows","BankLines","StatementImports"]);
        tables=tables.Where(t=>Convert.ToInt64(Scalar(DatabasePath,$"SELECT COUNT(*) FROM sqlite_schema WHERE type='table' AND name='{t}';"))>0).ToList();
        foreach(string table in tables)
            if(Convert.ToInt64(Scalar(DatabasePath,$"SELECT COUNT(*) FROM {table};"))!=0)throw new InvalidOperationException("An older-layout fixture cannot discard newer records.");
        bool dropEvidence=version is >=20 and <22 && Convert.ToInt64(Scalar(DatabasePath,"SELECT COUNT(*) FROM pragma_table_info('StatementImports') WHERE name='EvidenceId';"))>0;
        if(dropEvidence && Convert.ToInt64(Scalar(DatabasePath,"SELECT COUNT(*) FROM StatementImports WHERE EvidenceId<>'';"))>0)
            throw new InvalidOperationException("An older-layout fixture cannot discard newer statement evidence.");
        string drops=string.Join(" ",tables.Select(t=>$"DROP TABLE {t};"));
        string oldIndexes="""
            DROP TRIGGER IF EXISTS StatementImports_ActiveContent; DROP TRIGGER IF EXISTS BankLines_ActiveReference;
            DROP INDEX IF EXISTS StatementImports_Content; DROP INDEX IF EXISTS BankLines_Reference;
            CREATE UNIQUE INDEX StatementImports_Content ON StatementImports(AccountId,ContentHash) WHERE ContentHash IS NOT NULL;
            CREATE UNIQUE INDEX BankLines_Reference ON BankLines(AccountId,Reference) WHERE ReliableReference=1;
            """;
        Execute($"BEGIN IMMEDIATE; {(version<25?oldIndexes:"")} {drops} {(dropEvidence?"ALTER TABLE StatementImports DROP COLUMN EvidenceId;":"")} PRAGMA user_version={version}; COMMIT;");
    }

    internal string Hash() => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(DatabasePath)));

    internal string[] Snapshots() => Directory.Exists(BackupFolder)
        ? Directory.GetFiles(BackupFolder, "*.db") : [];

    internal static object? Scalar(string path, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    public void Dispose()
    {
        string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"SarahBeautyStorageTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(Folder);
        if(!target.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unsafe fixture cleanup path.");
        Directory.Delete(target,recursive:true);
    }
}
