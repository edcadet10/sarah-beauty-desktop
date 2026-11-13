namespace SarahBeauty.Business.Security;

public sealed partial class OwnerAccess
{
    internal Action? AfterPassphraseVerified {get;set;}
    internal Action? BeforePassphraseChangeCommit {get;set;}
    public void ChangePassphrase(OwnerSession session,string currentPhrase,string newPhrase,string repeatedPhrase,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();store.Read(db=>{store.RequireSession(db,session);return true;});ValidateNewPhrase(currentPhrase,newPhrase,repeatedPhrase);
        Confirm(session,currentPhrase);AfterPassphraseVerified?.Invoke();cancellation.ThrowIfCancellationRequested();
        var credential=Passphrase.Hash(newPhrase);cancellation.ThrowIfCancellationRequested();
        store.Transaction(db=>
        {
            cancellation.ThrowIfCancellationRequested();store.RequireSession(db,session);
            db.Execute("UPDATE Owners SET Salt=$salt,Hash=$hash,Iterations=$iterations,FailedAttempts=0,LockedUntil=NULL WHERE Id=$owner AND BusinessId=$business AND Active=1;",
                ("$salt",credential.Salt),("$hash",credential.Hash),("$iterations",Passphrase.Iterations),("$owner",session.OwnerId),("$business",session.BusinessId));
            SarahBeauty.Business.Storage.BusinessStore.Audit(db,session,"Changed own sign-in passphrase",session.OwnerId);
            BeforePassphraseChangeCommit?.Invoke();cancellation.ThrowIfCancellationRequested();
            if(!session.IsActive)throw new UnauthorizedAccessException("Owner access ended before the passphrase change. Sign in again.");
            return true;
        });
        session.Lock();
    }
    private static void ValidateNewPhrase(string current,string value,string repeated)
    {
        if(value is null || value.Length is <15 or >256)throw new ArgumentException("Use a new word-based passphrase with 15 to 256 characters.");
        if(!string.Equals(value,repeated,StringComparison.Ordinal))throw new ArgumentException("The new passphrases do not match. Enter them again.");
        if(string.Equals(value,current,StringComparison.Ordinal))throw new ArgumentException("Choose a different passphrase from the current one.");
    }
}
