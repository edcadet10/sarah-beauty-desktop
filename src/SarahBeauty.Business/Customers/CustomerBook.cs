using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Customers;

public sealed class CustomerBook(BusinessStore store)
{
    public List<Customer> Search(OwnerSession session, string search = "", bool includeArchived = false) => store.Read(db =>
    {
        store.RequireSession(db, session);
        return db.Read("""
            SELECT DataJson FROM Customers WHERE BusinessId=$business AND ($archived=1 OR Archived=0)
            AND instr(SearchName,$search)>0 ORDER BY SearchName,Id;
            """, r => JsonSerializer.Deserialize<Customer>(r.GetString(0))!,
            ("$business", session.BusinessId), ("$archived", includeArchived ? 1 : 0), ("$search", search.Trim().ToUpperInvariant()));
    });

    public Customer Save(OwnerSession session, Customer value)
    {
        value = CustomerValidation.Normalize(value);
        bool creating = value.Id.Length == 0;
        value = value with { Id = creating ? Guid.NewGuid().ToString() : value.Id };
        return store.Write(session, "Save customer", value.Id, db => Save(db, session, value, creating));
    }

    private static Customer Save(DbWork db, OwnerSession session, Customer value, bool creating)
    {
        Customer? previous = creating ? null : Find(db, session, value.Id);
        if (!creating && previous is null) throw new ArgumentException("The customer could not be found.");
        var saved = value with { Revision = value.Revision + 1 };
        int changed = SaveRow(db, session, saved, creating, value.Revision);
        if (changed != 1) throw new InvalidOperationException("This customer changed elsewhere. Reload it before saving.");
        if (previous is null || previous.PhotoPermission != saved.PhotoPermission
            || previous.PermissionScope != saved.PermissionScope || previous.PermissionNotes != saved.PermissionNotes)
            SavePermission(db, session, saved);
        return saved;
    }

    private static int SaveRow(DbWork db, OwnerSession session, Customer saved, bool creating, int previousRevision)
    {
        string sql = creating ? """
            INSERT INTO Customers (Id,BusinessId,SearchName,Archived,Revision,DataJson,CreatedAtUtc,CreatedBy,UpdatedAtUtc,UpdatedBy)
            VALUES ($id,$business,$name,$archived,$revision,$json,$at,$owner,$at,$owner);
            """ : """
            UPDATE Customers SET SearchName=$name,Archived=$archived,Revision=$revision,DataJson=$json,
            UpdatedAtUtc=$at,UpdatedBy=$owner WHERE Id=$id AND BusinessId=$business AND Revision=$previous;
            """;
        return db.Execute(sql, ("$id", saved.Id), ("$business", session.BusinessId),
            ("$name", saved.FullName.ToUpperInvariant()), ("$archived", saved.Archived ? 1 : 0),
            ("$revision", saved.Revision), ("$json", JsonSerializer.Serialize(saved)),
            ("$at", DateTimeOffset.UtcNow.ToString("O")), ("$owner", session.OwnerId), ("$previous", previousRevision));
    }

    internal static Customer? Find(DbWork db, OwnerSession session, string id) =>
        db.Scalar("SELECT DataJson FROM Customers WHERE Id=$id AND BusinessId=$business;",
            ("$id", id), ("$business", session.BusinessId)) is string json ? JsonSerializer.Deserialize<Customer>(json) : null;

    public List<PermissionHistory> Permissions(OwnerSession session, string customerId) => store.Read(db =>
    {
        store.RequireSession(db, session);
        return db.Read("""
            SELECT Status,Scope,Notes,AtUtc FROM CustomerPermissions
            WHERE CustomerId=$id AND BusinessId=$business ORDER BY rowid;
            """, r => new PermissionHistory(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3)),
            ("$id", customerId), ("$business", session.BusinessId));
    });

    private static void SavePermission(DbWork db, OwnerSession session, Customer customer) => db.Execute("""
        INSERT INTO CustomerPermissions (Id,CustomerId,BusinessId,Status,Scope,Notes,AtUtc,OwnerId)
        VALUES ($id,$customer,$business,$status,$scope,$notes,$at,$owner);
        """, ("$id", Guid.NewGuid().ToString()), ("$customer", customer.Id), ("$business", session.BusinessId),
        ("$status", customer.PhotoPermission), ("$scope", customer.PermissionScope), ("$notes", customer.PermissionNotes),
        ("$at", DateTimeOffset.UtcNow.ToString("O")), ("$owner", session.OwnerId));
}
