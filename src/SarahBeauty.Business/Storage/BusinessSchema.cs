namespace SarahBeauty.Business.Storage;

internal static class BusinessSchema
{
    internal const int Version = 40;

    internal const string Sql = """
        CREATE TABLE Business (
            Id TEXT PRIMARY KEY NOT NULL, DisplayName TEXT NOT NULL,
            SettingsJson TEXT NOT NULL DEFAULT '{}', Watermark INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE Owners (
            Id TEXT PRIMARY KEY NOT NULL, BusinessId TEXT NOT NULL REFERENCES Business(Id),
            Name TEXT COLLATE NOCASE NOT NULL UNIQUE, Salt TEXT NOT NULL, Hash TEXT NOT NULL,
            Iterations INTEGER NOT NULL, FailedAttempts INTEGER NOT NULL DEFAULT 0,
            LockedUntil TEXT, Active INTEGER NOT NULL DEFAULT 1 CHECK(Active IN (0,1))
        );
        CREATE TABLE Audit (
            Sequence INTEGER PRIMARY KEY AUTOINCREMENT, BusinessId TEXT NOT NULL REFERENCES Business(Id),
            OwnerId TEXT NOT NULL REFERENCES Owners(Id), Action TEXT NOT NULL, RecordId TEXT,
            AtUtc TEXT NOT NULL, Details TEXT NOT NULL DEFAULT ''
        );
        """;
}
