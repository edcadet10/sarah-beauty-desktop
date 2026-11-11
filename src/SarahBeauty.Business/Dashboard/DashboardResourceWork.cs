using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Dashboard;

internal static class DashboardResourceWork
{
    internal static DashboardSupply[] Supplies(DbWork db,BusinessStore store,OwnerSession owner,DateOnly asOf)=>new RecordStore<Supply>(store,"Supply").List(db,owner)
        .Where(s=>!s.Archived).Select(s=>new DashboardSupply(s.Id,s.Name,s.Brand,s.Shade,s.SupplyStatus,s.Location,s.ExpiryDate,s.SupplyStatus=="Low",s.SupplyStatus=="Finished",s.ExpiryDate<=asOf)).ToArray();

    internal static IEnumerable<DashboardWorkItem> Trips(DbWork db,BusinessStore store,OwnerSession owner,DateOnly asOf)
    {
        var trips=new RecordStore<MileageTrip>(store,"MileageTrip").List(db,owner).Where(t=>!t.Archived && t.TripDate<=asOf);var items=new List<DashboardWorkItem>();
        foreach(var trip in trips.Where(t=>t.ReimbursementStatus!="Not requested"))
        {
            if(trip.ReimbursementStatus=="Unreviewed" || trip.RequestedReimbursementCents is null)
            {items.Add(new("Trip review",trip.Id,trip.Name,"Resources",trip.Revision,["Reimbursement decision or requested amount remains unconfirmed."]));continue;}
            var review=new TripReimbursementSources(store).Load(db,owner,trip.Id,asOf);long due=checked(trip.RequestedReimbursementCents.Value-review.CurrentClaimLinkedCents);
            var reasons=new List<string>();if(due>0)reasons.Add("Reviewed reimbursement remains partly or wholly unlinked to an actual approved payment.");
            if(review.OtherBasisLinkedCents>0)reasons.Add("Payment attribution remains on an earlier claim basis.");
            if(reasons.Count>0)items.Add(new("Trip reimbursement",trip.Id,trip.Name,"Resources",trip.Revision,reasons.ToArray(),due));
        }
        return items;
    }
}
