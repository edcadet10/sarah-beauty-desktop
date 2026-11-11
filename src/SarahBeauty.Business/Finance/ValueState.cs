using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class ValueState
{
    internal static long Generation(DbWork db,OwnerSession owner,string kind,string id)=>Convert.ToInt64(db.Scalar(
        "SELECT Generation FROM ValueState WHERE BusinessId=$business AND Kind=$kind AND SourceId=$id;",
        ("$business",owner.BusinessId),("$kind",kind),("$id",id))??0L);

    internal static void Advance(DbWork db,OwnerSession owner,string kind,string id)=>db.Execute("""
        INSERT INTO ValueState (BusinessId,Kind,SourceId,Generation) VALUES ($business,$kind,$id,1)
        ON CONFLICT(BusinessId,Kind,SourceId) DO UPDATE SET Generation=Generation+1;
        """,("$business",owner.BusinessId),("$kind",kind),("$id",id));
}
