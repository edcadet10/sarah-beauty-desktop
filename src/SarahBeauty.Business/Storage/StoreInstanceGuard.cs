namespace SarahBeauty.Business.Storage;

internal static class StoreInstanceGuard
{
    internal const string Migration="""
        INSERT INTO AppMetadata(Key,Value) VALUES('WritableInstanceId',lower(hex(randomblob(16)))) ON CONFLICT(Key) DO NOTHING;
        INSERT INTO AppMetadata(Key,Value) VALUES('WritableInstanceState','Active') ON CONFLICT(Key) DO NOTHING;
        """;
    internal static string Read(DbWork db,string storeId,string mode="TEST",string requiredState="Active")
    {
        var values=db.Read("SELECT Key,Value FROM AppMetadata WHERE Key IN ('StoreId','StoreMode','WritableInstanceId','WritableInstanceState');",r=>(Key:r.GetString(0),Value:r.GetString(1))).ToDictionary(p=>p.Key,p=>p.Value);
        if(values.Count!=4 || values["StoreId"]!=storeId || values["StoreMode"]!=mode || !Guid.TryParseExact(values["WritableInstanceId"],"N",out var id) || id==Guid.Empty || values["WritableInstanceState"] is not("Active" or "Prepared" or "Retired"))throw new InvalidDataException("The writable store instance metadata is missing or invalid. Preserve this store and review recovery options.");
        if(values["WritableInstanceState"]!=requiredState)throw Unavailable();return values["WritableInstanceId"];
    }
    internal static void Require(DbWork db,string storeId,string instanceId,string requiredState="Active",BusinessMode mode=BusinessMode.Test)
    {
        if(Read(db,storeId,BusinessModes.StoredName(mode),requiredState)!=instanceId || Convert.ToInt32(db.Scalar("PRAGMA user_version;"))!=BusinessSchema.Version)throw Unavailable();
    }
    private static UnauthorizedAccessException Unavailable()=>new("This writable store instance changed or was retired. Open the active business store and sign in again.");
}
