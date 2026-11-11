using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

internal static class RestoredDatabaseChecks
{
    internal static void Verify(string path,ArchiveManifest manifest)
    {
        using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Mode=SqliteOpenMode.ReadOnly,Pooling=false,ForeignKeys=true}.ToString());connection.Open();var db=new DbWork(connection);
        if((string?)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreId';")!=manifest.StoreId || (string?)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!=manifest.Mode || Convert.ToInt32(db.Scalar("PRAGMA user_version;"))!=manifest.SchemaVersion)throw new InvalidDataException("The restored database identity, mode, or schema differs from the manifest.");
        if((long)db.Scalar("SELECT COUNT(*) FROM Business;")! !=1 || (string?)db.Scalar("SELECT DisplayName FROM Business WHERE Id=$id;",("$id",manifest.BusinessId))!=manifest.BusinessName || (long?)db.Scalar("SELECT Watermark FROM Business WHERE Id=$id;",("$id",manifest.BusinessId))!=manifest.Watermark)throw new InvalidDataException("The restored business or watermark differs from the manifest.");
        var facts=ArchiveDatabaseChecks.Read(db);var expected=manifest.Facts;
        if(facts.MovementCount!=expected.MovementCount || facts.JournalLineCount!=expected.JournalLineCount || facts.TotalDebits!=expected.TotalDebits || facts.TotalCredits!=expected.TotalCredits || !facts.Tables.SequenceEqual(expected.Tables))throw new InvalidDataException("The restored database counts or journal totals differ from the manifest.");
        var references=ArchiveReferences.Read(db,manifest.BusinessId);var files=manifest.Files.Where(f=>f.Kind!="Database").ToDictionary(f=>f.RelativePath,StringComparer.Ordinal);
        if(references.Length!=files.Count)throw new InvalidDataException("The restored file list differs from its database references.");
        foreach(var reference in references)
            if(!files.TryGetValue(ArchivePaths.Normalize(reference.RelativePath),out var file) || file.Kind!=reference.Kind || file.Sha256!=reference.ExpectedHash || reference.ExpectedBytes.HasValue && file.ByteLength!=reference.ExpectedBytes)throw new InvalidDataException("A restored managed file differs from its database reference.");
    }
}
