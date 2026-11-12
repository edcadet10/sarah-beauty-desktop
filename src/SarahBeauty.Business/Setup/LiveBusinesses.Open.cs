using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Setup;

public sealed partial class LiveBusinesses
{
    public bool Exists
    {
        get{var paths=new LiveBusinessPaths(parent);paths.RequireNoLinks();return Directory.Exists(paths.Primary)||File.Exists(paths.Primary);}
    }
    public BusinessStore Open()
    {
        var paths=new LiveBusinessPaths(parent);paths.RequireNoLinks();
        if(!File.Exists(paths.Database))throw new FileNotFoundException("The existing LIVE database is unavailable. Preserve its folder and review recovery; opening never creates a replacement.");
        var store=new BusinessStore(paths.Database,BusinessMode.Live);
        store.ReadSnapshot(db=>
        {
            if((string?)db.Scalar("PRAGMA integrity_check;")!="ok"||db.Read("PRAGMA foreign_key_check;",_=>true).Count!=0)throw new InvalidDataException("The LIVE database needs integrity review. Preserve it and review recovery.");
            var businesses=db.Read("SELECT Id FROM Business;",r=>r.GetString(0));
            if(businesses.Count!=1||Convert.ToInt64(db.Scalar("SELECT count(*) FROM Owners WHERE BusinessId=$id AND Active=1;",("$id",businesses[0])))!=2||BusinessProfileQueries.Read(db,businesses[0]).Count==0)throw new InvalidDataException("The LIVE business setup is incomplete or inconsistent. Preserve this copy and review recovery.");
            return true;
        });return store;
    }
}
