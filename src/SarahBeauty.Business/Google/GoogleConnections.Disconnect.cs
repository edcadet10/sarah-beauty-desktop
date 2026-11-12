using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed record GoogleLocalDisconnectResult(GoogleConnectionRecord Record,bool DeviceTokenCleared);
public sealed partial class GoogleConnections
{
    public GoogleLocalDisconnectResult DisconnectLocal(OwnerSession owner,string expectedConnectionId)
    {
        var saved=_store.Transaction(db=>
        {
            _store.RequireSession(db,owner);GoogleConnectionRecords.RequireExpected(db,_business,_store.InstanceId,expectedConnectionId);
            var current=GoogleConnectionRecords.Read(db,_business,_store.InstanceId)??throw new ArgumentException("Choose a recorded Google connection.");if(current.Status=="Disconnected")return current;
            var disconnected=current with{Id=Guid.NewGuid().ToString(),PreviousConnectionId=current.Id,Status="Disconnected",DisconnectedBy=owner.OwnerId,DisconnectedAtUtc=DateTimeOffset.UtcNow.ToString("O")};
            GoogleConnectionRecords.Save(db,disconnected);BusinessStore.Audit(db,owner,"Disconnected Google locally",disconnected.Id);return disconnected;
        });
        FailurePoint?.Invoke("Local disconnection recorded");
        try
        {
            _files.Delete(DeviceCredentialKind.GoogleToken,db=>
            {
                var current=GoogleConnectionRecords.Read(db,_business,_store.InstanceId);if(current?.Id!=saved.Id || current.Status!="Disconnected")throw new ArgumentException("The Google connection changed before token cleanup. Reload its state.");
            });return new(saved,true);
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or ArgumentException or Microsoft.Data.Sqlite.SqliteException){return new(saved,false);}
    }
}
