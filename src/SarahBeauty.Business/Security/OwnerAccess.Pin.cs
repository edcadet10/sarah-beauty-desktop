namespace SarahBeauty.Business.Security;

public sealed partial class OwnerAccess
{
    internal OwnerSession SignInWithPin(string ownerId,string pin,OwnerPins pins)
    {
        const string rejected="The PIN was not accepted, or sign-in is temporarily locked. You can use your password instead.";
        var credential=store.Read(db=>ReadCredential(db,ownerId));
        if(!MayAuthenticate(credential))throw new UnauthorizedAccessException(rejected);
        AfterCredentialsRead?.Invoke();
        string stamp=OwnerCredentials.Stamp(credential!.Salt,credential.Hash,credential.Iterations);
        var result=pins.Review(ownerId,value=>
        {
            bool valid=value?.Verify(ownerId,stamp,pin)==true;
            return store.Transaction(db=>CompleteAuthentication(db,ownerId,credential,valid));
        });
        return result??throw new UnauthorizedAccessException(rejected);
    }
}
