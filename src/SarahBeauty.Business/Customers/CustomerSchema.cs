namespace SarahBeauty.Business.Customers;

internal static class CustomerSchema
{
    internal const string Sql = """
        CREATE TABLE Customers (
            Id TEXT PRIMARY KEY NOT NULL, BusinessId TEXT NOT NULL REFERENCES Business(Id),
            SearchName TEXT NOT NULL, Archived INTEGER NOT NULL CHECK(Archived IN (0,1)),
            Revision INTEGER NOT NULL CHECK(Revision>0), DataJson TEXT NOT NULL CHECK(json_valid(DataJson)),
            CreatedAtUtc TEXT NOT NULL, CreatedBy TEXT NOT NULL REFERENCES Owners(Id),
            UpdatedAtUtc TEXT NOT NULL, UpdatedBy TEXT NOT NULL REFERENCES Owners(Id)
        );
        CREATE INDEX Customers_Search ON Customers(BusinessId,Archived,SearchName);
        CREATE TABLE CustomerPermissions (
            Id TEXT PRIMARY KEY NOT NULL, CustomerId TEXT NOT NULL REFERENCES Customers(Id),
            BusinessId TEXT NOT NULL REFERENCES Business(Id), Status TEXT NOT NULL,
            Scope TEXT NOT NULL, Notes TEXT NOT NULL, AtUtc TEXT NOT NULL,
            OwnerId TEXT NOT NULL REFERENCES Owners(Id)
        );
        """;
}
