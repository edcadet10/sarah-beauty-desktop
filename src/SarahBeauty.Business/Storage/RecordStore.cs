using System.Text.Json;
using SarahBeauty.Business.Security;

namespace SarahBeauty.Business.Storage;

internal sealed class RecordStore<T>(BusinessStore store, string kind) where T : BusinessRecord
{
    internal List<T> List(OwnerSession session) => store.Read(db =>
    {
        store.RequireSession(db, session);
        return List(db, session);
    });

    internal List<T> List(DbWork db, OwnerSession session) => db.Read(
        "SELECT DataJson FROM Records WHERE BusinessId=$business AND Kind=$kind ORDER BY CreatedAtUtc,Id;",
        r => JsonSerializer.Deserialize<T>(r.GetString(0))!, ("$business", session.BusinessId), ("$kind", kind));

    internal T Get(DbWork db, OwnerSession session, string id) =>
        db.Scalar("SELECT DataJson FROM Records WHERE Id=$id AND BusinessId=$business AND Kind=$kind;",
            ("$id", id), ("$business", session.BusinessId), ("$kind", kind)) is string json
            ? JsonSerializer.Deserialize<T>(json)! : throw new ArgumentException($"The {kind.ToLowerInvariant()} could not be found.");

    internal T Save(DbWork db, OwnerSession session, T value)
    {
        bool creating = value.Revision == 0;
        if (string.IsNullOrWhiteSpace(value.Id)) throw new ArgumentException("A permanent record ID is required.");
        var saved = (T)(value with { Revision = checked(value.Revision + 1) });
        string sql = creating ? """
            INSERT INTO Records (Id,BusinessId,Kind,Revision,DataJson,CreatedAtUtc,CreatedBy,UpdatedAtUtc,UpdatedBy)
            VALUES ($id,$business,$kind,$revision,$json,$at,$owner,$at,$owner);
            """ : """
            UPDATE Records SET Revision=$revision,DataJson=$json,UpdatedAtUtc=$at,UpdatedBy=$owner
            WHERE Id=$id AND BusinessId=$business AND Kind=$kind AND Revision=$previous;
            """;
        int changed = db.Execute(sql, ("$id", saved.Id), ("$business", session.BusinessId), ("$kind", kind),
            ("$revision", saved.Revision), ("$json", JsonSerializer.Serialize(saved)), ("$at", DateTimeOffset.UtcNow.ToString("O")),
            ("$owner", session.OwnerId), ("$previous", value.Revision));
        if (changed != 1) throw new InvalidOperationException("This record changed elsewhere. Reload it before saving.");
        return saved;
    }
}
