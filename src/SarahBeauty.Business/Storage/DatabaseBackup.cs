using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace SarahBeauty_Desktop.Storage;

public static class DatabaseBackup
{
    public static string CreateVerifiedSnapshot(string databasePath)
    {
        string sourcePath = Path.GetFullPath(databasePath);
        string sourceFolder = Path.GetDirectoryName(sourcePath)
            ?? throw new InvalidOperationException(
                "The database folder could not be determined.");

        using var source = CreateConnection(sourcePath, SqliteOpenMode.ReadOnly);
        source.Open();

        string backupFolder = Path.Combine(sourceFolder, "Backups");
        Directory.CreateDirectory(backupFolder);

        string fileName =
            $"sarahbeauty-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.db";
        string backupPath = Path.Combine(backupFolder, fileName);
        string partialPath = backupPath + ".partial";

        using (new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
        }

        try
        {
            CopySnapshot(source, partialPath);
            VerifySnapshot(partialPath);
            File.Move(partialPath, backupPath);
            return backupPath;
        }
        finally
        {
            DeletePartialSnapshot(partialPath);
        }
    }

    private static void CopySnapshot(SqliteConnection source, string partialPath)
    {
        using var destination = CreateConnection(partialPath, SqliteOpenMode.ReadWrite);
        destination.Open();
        source.BackupDatabase(destination);

        // A recovery snapshot must open independently of WAL/SHM sidecar files.
        using var command = destination.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = DELETE;";
        if (!string.Equals(command.ExecuteScalar() as string, "delete", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The backup could not be made self-contained.");
        }
    }

    private static void VerifySnapshot(string partialPath)
    {
        using var verification = CreateConnection(partialPath, SqliteOpenMode.ReadOnly);
        verification.Open();
        using var command = verification.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        if (!string.Equals(command.ExecuteScalar() as string, "ok", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The backup failed its database integrity check.");
        }

        command.CommandText = "PRAGMA foreign_key_check;";
        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            throw new InvalidOperationException("The backup contains invalid foreign-key references.");
        }
    }

    private static SqliteConnection CreateConnection(string path, SqliteOpenMode mode) =>
        new(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            DefaultTimeout = 5,
            Pooling = false
        }.ToString());

    private static void DeletePartialSnapshot(string partialPath)
    {
        try
        {
            File.Delete(partialPath);
        }
        catch (IOException)
        {
            // A leftover .partial file is never advertised as a verified snapshot.
        }
        catch (UnauthorizedAccessException)
        {
            // Preserve the original failure if Windows also refuses cleanup.
        }
    }
}
