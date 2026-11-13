using SarahBeauty.Business.Setup;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Security;

public sealed partial class OwnerAccess
{
    internal const string SimpleSetupAction = "Start business with one user";

    public OwnerSession Start(string businessName, string username, string password)
    {
        RequireStart(businessName, username, password);
        var credential = Passphrase.Hash(password);
        return store.Transaction(db =>
        {
            if (db.Scalar("SELECT Id FROM Business;") is not null)
                throw new InvalidOperationException("This business is already set up. Sign in to continue.");
            string businessId = Guid.NewGuid().ToString();
            db.Execute("INSERT INTO Business(Id,DisplayName) VALUES($id,$name);",
                ("$id", businessId), ("$name", businessName.Trim()));
            string id = AddOwner(db, businessId, username.Trim(), credential);
            var session = new OwnerSession(id, username.Trim(), businessId, store.StoreId, store.InstanceId,
                credentialStamp: OwnerCredentials.Stamp(credential.Salt, credential.Hash, Passphrase.Iterations), mode: store.Mode);
            BusinessStore.Audit(db, session, SimpleSetupAction, businessId);
            return session;
        });
    }

    internal static void RequireStart(string businessName, string username, string password)
    {
        BusinessProfileRules.Normalize(new() { DisplayName = businessName });
        RequireUsername(username);
        Passphrase.Require(password);
    }

    private static void RequireUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length > 100 || username.Any(char.IsControl))
            throw new ArgumentException("Enter a username with 1 to 100 characters.");
    }

    public string AddUser(OwnerSession owner, string currentPassword, string username, string password)
    {
        RequireUsername(username);
        Passphrase.Require(password);
        Confirm(owner, currentPassword);
        var credential = Passphrase.Hash(password);
        return store.Write(owner, "Add owner access", null, db =>
        {
            var names = db.Read("SELECT Name FROM Owners WHERE BusinessId=$business;", r => r.GetString(0), ("$business", owner.BusinessId));
            if (names.Count >= 2) throw new ArgumentException("This business already has two users.");
            if (names.Any(name => string.Equals(name, username.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Choose a different username for the other user.");
            return AddOwner(db, owner.BusinessId, username.Trim(), credential);
        });
    }
}
