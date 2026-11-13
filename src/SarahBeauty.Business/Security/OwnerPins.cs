using System.Security.Cryptography;
using System.Text.Json;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Security;

public sealed class OwnerPins
{
    private readonly BusinessStore _store;
    private readonly string _root;
    public OwnerPins(BusinessStore store):this(store,Path.Combine(DeviceCredentialFiles.DefaultRoot(),"OwnerPins")){}
    internal OwnerPins(BusinessStore store,string root){_store=store;_root=root;}

    public bool IsConfigured(string ownerId)
    {
        string? stamp=_store.Read(db=>db.Read("SELECT Salt,Hash,Iterations FROM Owners WHERE Id=$owner AND Active=1;",
            r=>OwnerCredentials.Stamp(r.GetString(0),r.GetString(1),r.GetInt32(2)),("$owner",ownerId)).SingleOrDefault());
        if(stamp is null)return false;
        try
        {
            byte[]? clear=Files(ownerId).Read(DeviceCredentialKind.OwnerPin);
            try{var value=OwnerPinMaterial.Read(clear);return value is {Version:1}&&value.OwnerId==ownerId&&value.CredentialStamp==stamp;}
            finally{if(clear is not null)CryptographicOperations.ZeroMemory(clear);}
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or InvalidDataException){return false;}
    }

    public void Set(OwnerSession owner,string password,string pin,string repeatedPin,CancellationToken cancellation=default)
    {
        if(!OwnerPinMaterial.ValidPin(pin))throw new ArgumentException("Use a PIN with 6 to 12 digits (0–9).");
        if(pin!=repeatedPin)throw new ArgumentException("The PINs do not match. Enter them again.");
        Authorize(owner,password,cancellation);byte[] clear=JsonSerializer.SerializeToUtf8Bytes(OwnerPinMaterial.Create(owner,pin));
        try{cancellation.ThrowIfCancellationRequested();Files(owner.OwnerId).Write(DeviceCredentialKind.OwnerPin,clear,db=>{cancellation.ThrowIfCancellationRequested();_store.RequireSession(db,owner);});}
        finally{CryptographicOperations.ZeroMemory(clear);}
    }

    public void Remove(OwnerSession owner,string password,CancellationToken cancellation=default)
    {
        Authorize(owner,password,cancellation);
        Files(owner.OwnerId).Delete(DeviceCredentialKind.OwnerPin,db=>{cancellation.ThrowIfCancellationRequested();_store.RequireSession(db,owner);});
    }
    public OwnerSession SignIn(string ownerId,string pin)=>new OwnerAccess(_store).SignInWithPin(ownerId,pin,this);
    internal T Review<T>(string ownerId,Func<OwnerPinMaterial?,T> review)
    {
        try{return Files(ownerId).Review(DeviceCredentialKind.OwnerPin,clear=>review(OwnerPinMaterial.Read(clear)));}
        catch(InvalidDataException){throw new UnauthorizedAccessException("The local PIN could not be read. Sign in with your password and set the PIN again.");}
    }

    private void Authorize(OwnerSession owner,string password,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();_store.Read(db=>{_store.RequireSession(db,owner);return true;});
        new OwnerAccess(_store).Confirm(owner,password);cancellation.ThrowIfCancellationRequested();
    }
    private DeviceCredentialFiles Files(string ownerId)
    {
        if(!Guid.TryParseExact(ownerId,"D",out var id))throw new ArgumentException("Select an owner of this business.");
        return new(_store,Path.Combine(_root,id.ToString("D")));
    }
}
