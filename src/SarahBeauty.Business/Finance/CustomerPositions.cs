using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class CustomerPositions
{
    internal static List<CustomerPosition> Read(DbWork db,OwnerSession owner,DateOnly through,List<DocumentVersion> versions)
    {
        var dues=versions.Where(v=>v.Snapshot.Kind==DocumentKind.Invoice && v.Status!="Void")
            .SelectMany(v=>ValueAvailability.Invoice(db,owner,v.Id,through)).ToList();
        var payments=db.Read("SELECT Id,CustomerId FROM Movements WHERE BusinessId=$business AND Kind='Receipt' AND Date<=$date;",
            r=>(Id:r.GetString(0),Customer:r.GetString(1)),("$business",owner.BusinessId),("$date",through.ToString("yyyy-MM-dd")));
        var credits=CreditValues.List(db,owner).Where(c=>c.IssueDate<=through).ToList();
        var ids=dues.Select(d=>d.CustomerId).Concat(payments.Select(p=>p.Customer)).Concat(credits.Select(c=>c.CustomerId)).Distinct();
        return ids.Select(id=>
        {
            var open=dues.Where(d=>d.CustomerId==id).ToList();
            string name=CustomerBook.Find(db,owner,id)?.FullName??"Customer unavailable";
            long unused=payments.Where(p=>p.Customer==id).Sum(p=>ValueAvailability.Payment(db,owner,p.Id,through).Cents);
            long credit=credits.Where(c=>c.CustomerId==id).Sum(c=>CreditValues.Available(db,owner,c.Id,through).Cents);
            return new CustomerPosition(id,name,open.Sum(d=>d.Cents),open.Where(d=>d.DueDate<through).Sum(d=>d.Cents),
                open.Where(d=>d.DueDate==through).Sum(d=>d.Cents),open.Where(d=>d.DueDate>through).Sum(d=>d.Cents),
                open.Where(d=>d.DueDate is null).Sum(d=>d.Cents),unused,credit);
        }).OrderBy(c=>c.Name).ThenBy(c=>c.CustomerId).ToList();
    }
}
