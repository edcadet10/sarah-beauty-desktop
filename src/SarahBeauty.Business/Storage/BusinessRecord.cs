namespace SarahBeauty.Business.Storage;

public abstract record BusinessRecord
{
    public string Id { get; init; } = "";
    public int Revision { get; init; }
    public string Name { get; init; } = "";
    public bool Archived { get; init; }
    public override string ToString() => Name;
}

internal static class RecordStoreSchema
{
    internal const string Sql = """
        CREATE TABLE Records (
            Id TEXT PRIMARY KEY NOT NULL, BusinessId TEXT NOT NULL REFERENCES Business(Id), Kind TEXT NOT NULL,
            Revision INTEGER NOT NULL CHECK(Revision>0), DataJson TEXT NOT NULL CHECK(json_valid(DataJson)),
            CreatedAtUtc TEXT NOT NULL, CreatedBy TEXT NOT NULL REFERENCES Owners(Id),
            UpdatedAtUtc TEXT NOT NULL, UpdatedBy TEXT NOT NULL REFERENCES Owners(Id)
        );
        CREATE INDEX Records_Kind ON Records(BusinessId,Kind);
        """;
}
