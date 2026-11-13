using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Security;

public sealed class OwnerAccess(BusinessStore store)
{
    internal Action? AfterCredentialsRead {get;set;}
    public bool NeedsSetup => store.Read(db => db.Scalar("SELECT Id FROM Business;") is null);

    public List<(string Id, string Name)> Owners() => store.Read(db => db.Read(
        "SELECT Id,Name FROM Owners WHERE Active=1 ORDER BY Name;", r => (r.GetString(0), r.GetString(1))));

    public bool IsSessionCurrent(OwnerSession session)
    {
        if(!session.IsActive)return false;
        try{return store.Read(db=>{store.RequireCurrentSession(db,session);return true;});}
        catch(UnauthorizedAccessException){return false;}
    }
    public bool TryRecordActivity(OwnerSession session)
    {
        if(!session.IsActive)return false;
        try{return store.Read(db=>{store.RequireSession(db,session);return true;});}
        catch(UnauthorizedAccessException){return false;}
    }

    public OwnerSession Setup(string businessName, string firstName, string firstPhrase,
        string secondName, string secondPhrase)
    {
        if (new[] { businessName, firstName, secondName }.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Enter a business name and both owner names.");
        if (string.Equals(firstName.Trim(), secondName.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Use a distinct name for each owner.");
        var first = Passphrase.Hash(firstPhrase);
        var second = Passphrase.Hash(secondPhrase);
        return store.Transaction(db =>
        {
            if (db.Scalar("SELECT Id FROM Business;") is not null)
                throw new InvalidOperationException("This business is already set up.");
            string businessId = Guid.NewGuid().ToString();
            db.Execute("INSERT INTO Business (Id,DisplayName) VALUES ($id,$name);",
                ("$id", businessId), ("$name", businessName.Trim()));
            string ownerId = AddOwner(db, businessId, firstName.Trim(), first);
            AddOwner(db, businessId, secondName.Trim(), second);
            var session = new OwnerSession(ownerId, firstName.Trim(), businessId, store.StoreId,store.InstanceId,credentialStamp:OwnerCredentials.Stamp(first.Salt,first.Hash,Passphrase.Iterations));
            BusinessStore.Audit(db, session, "Business setup", businessId);
            return session;
        });
    }

    public OwnerSession SignIn(string ownerId, string phrase)
    {
        var credential=store.Read(db=>ReadCredential(db,ownerId));
        if(!MayAuthenticate(credential))throw new UnauthorizedAccessException("The passphrase was not accepted, or sign-in is temporarily locked.");
        AfterCredentialsRead?.Invoke();
        bool valid=Passphrase.Verify(phrase,credential!.Salt,credential.Hash,credential.Iterations);
        var result = store.Transaction(db => CompleteAuthentication(db,ownerId,credential,valid));
        return result ?? throw new UnauthorizedAccessException("The passphrase was not accepted, or sign-in is temporarily locked.");
    }

    public void Confirm(OwnerSession session, string phrase)
    {
        if (session.StoreId != store.StoreId || session.InstanceId!=store.InstanceId || !session.IsActive)
            throw new UnauthorizedAccessException("Sign in to this business to continue.");
        var verified = SignIn(session.OwnerId, phrase);
        try
        {
            if (verified.BusinessId != session.BusinessId) throw new UnauthorizedAccessException();
            store.Read(db=>{store.RequireSession(db,session);return true;});
        }
        finally{verified.Lock();}
    }

    private static OwnerCredential? ReadCredential(DbWork db,string id)=>db.Read("""
        SELECT BusinessId,Name,Salt,Hash,Iterations,FailedAttempts,LockedUntil
        FROM Owners WHERE Id=$id AND Active=1;
        """,r=>new OwnerCredential(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetInt32(4),r.GetInt32(5),r.IsDBNull(6)?null:r.GetString(6)),("$id",id)).SingleOrDefault();

    private static bool MayAuthenticate(OwnerCredential? owner)=>owner is not null && (owner.LockedUntil is null || DateTimeOffset.Parse(owner.LockedUntil)<=DateTimeOffset.UtcNow);

    private OwnerSession? CompleteAuthentication(DbWork db,string id,OwnerCredential reviewed,bool valid)
    {
        // Expensive verification ran without a writer reservation. Recheck the actual credential and current lockout before recording its result.
        var owner=ReadCredential(db,id);
        if(!MayAuthenticate(owner) || owner!.BusinessId!=reviewed.BusinessId || owner.Salt!=reviewed.Salt || owner.Hash!=reviewed.Hash || owner.Iterations!=reviewed.Iterations)return null;
        RecordAttempt(db, id, valid, owner.Failures);
        return valid ? new OwnerSession(id, owner.Name, owner.BusinessId, store.StoreId,store.InstanceId,credentialStamp:OwnerCredentials.Stamp(owner.Salt,owner.Hash,owner.Iterations)) : null;
    }

    private static void RecordAttempt(DbWork db, string id, bool valid, int failures)
    {
        int attempts = valid ? 0 : failures + 1;
        string? locked = attempts >= 5 ? DateTimeOffset.UtcNow.AddSeconds(30).ToString("O") : null;
        db.Execute("UPDATE Owners SET FailedAttempts=$attempts,LockedUntil=$locked WHERE Id=$id;",
            ("$attempts", attempts), ("$locked", locked), ("$id", id));
    }

    private static string AddOwner(DbWork db, string businessId, string name, (string Salt, string Hash) credential)
    {
        string id = Guid.NewGuid().ToString();
        db.Execute("""
            INSERT INTO Owners (Id,BusinessId,Name,Salt,Hash,Iterations) VALUES ($id,$business,$name,$salt,$hash,$iterations);
            """, ("$id", id), ("$business", businessId), ("$name", name),
            ("$salt", credential.Salt), ("$hash", credential.Hash), ("$iterations", Passphrase.Iterations));
        return id;
    }

    private sealed record OwnerCredential(string BusinessId, string Name, string Salt, string Hash,
        int Iterations, int Failures, string? LockedUntil);
}
