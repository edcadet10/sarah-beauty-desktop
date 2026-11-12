using SarahBeauty.Business.Storage;
using SarahBeauty.Business.Recovery;

namespace SarahBeauty.Business.Setup;

public sealed partial class LiveBusinesses
{
    public bool Exists
    {
        get{var paths=new LiveBusinessPaths(parent);paths.RequireNoLinks();return Directory.Exists(paths.Primary)||File.Exists(paths.Primary)||File.Exists(paths.Selection)||Directory.Exists(paths.Selection);}
    }
    public BusinessStore Open()
    {
        var paths=new LiveBusinessPaths(parent);paths.RequireNoLinks();
        string database=new ActiveBusinessStores(parent,BusinessMode.Live).ResolveDatabase();
        if(!File.Exists(database))throw new FileNotFoundException("The existing LIVE database is unavailable. Preserve its folder and review recovery; opening never creates a replacement.");
        var store=new BusinessStore(database,BusinessMode.Live);
        store.ReadSnapshot(db=>
        {
            if((string?)db.Scalar("PRAGMA integrity_check;")!="ok"||db.Read("PRAGMA foreign_key_check;",_=>true).Count!=0)throw new InvalidDataException("The LIVE database needs integrity review. Preserve it and review recovery.");
            var businesses=db.Read("SELECT Id FROM Business;",r=>r.GetString(0));
            if(businesses.Count!=1||Convert.ToInt64(db.Scalar("SELECT count(*) FROM Owners WHERE BusinessId=$id AND Active=1;",("$id",businesses[0])))!=2)throw new InvalidDataException("The LIVE business setup is incomplete or inconsistent. Preserve this copy and review recovery.");
            RequireProfile(db,businesses[0],paths);
            return true;
        });return store;
    }
    private static void RequireProfile(DbWork db,string business,LiveBusinessPaths paths)
    {
        if(BusinessProfileQueries.Read(db,business).Count>0)return;
        var selection=ActiveStorePointer.Read(new(paths.Root,BusinessMode.Live));
        if(selection is not null&&ReadyStoreChecks.Read(db,selection.RequestId).SourceManifest!.SchemaVersion<39&&Convert.ToInt64(db.Scalar("SELECT count(*) FROM Audit WHERE BusinessId=$id AND Action='Confirm private business profile';",("$id",business)))==0)return;
        throw new InvalidDataException("The LIVE profile history is missing. Preserve this copy and review recovery.");
    }
}
