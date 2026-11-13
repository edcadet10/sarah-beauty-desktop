using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Dashboard;

internal static class DashboardUnclassifiedWork
{
    internal static IEnumerable<DashboardWorkItem> Read(DbWork db,OwnerSession owner,DateOnly asOf)
    {
        var rows=db.Read("SELECT Id,Memo FROM Movements WHERE BusinessId=$business AND Kind='UnclassifiedReceipt' AND Date<=$date;",
            r=>(Id:r.GetString(0),Name:r.GetString(1)),("$business",owner.BusinessId),("$date",asOf.ToStorageDate()));
        var items=new List<DashboardWorkItem>();
        foreach(var row in rows)
        {
            long remaining=checked(ValueAvailability.Payment(db,owner,row.Id,asOf).Cents+ValueAvailability.Funding(db,"PaymentId",row.Id,asOf).Cents);
            if(remaining>0)items.Add(new("Received-value review",row.Id,row.Name,"Money",0,["Unclassified received value remains, including any reserved credit share."],remaining));
        }
        return items;
    }
}
