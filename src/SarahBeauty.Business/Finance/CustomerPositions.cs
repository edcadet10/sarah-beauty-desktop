using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class CustomerPositions
{
    internal static List<CustomerPosition> Read(DbWork db,OwnerSession owner,DateOnly through,List<DocumentVersion> versions)=>Read(db,owner.BusinessId,through,versions);
    internal static List<CustomerPosition> Read(DbWork db,string businessId,DateOnly through,List<DocumentVersion> versions)
    {
        var dues=versions.Where(v=>v.Snapshot.Kind==DocumentKind.Invoice && InvoiceVisibility.At(db,businessId,v,through))
            .SelectMany(v=>ValueAvailability.InvoiceAsOf(db,businessId,v.Id,through)).ToList();
        var payments=db.Read("""
            SELECT M.Id,M.CustomerId,A.Type,A.Code FROM Movements M JOIN Accounts A ON A.Id=json_extract(M.DataJson,'$.CounterAccountId')
            WHERE M.BusinessId=$business AND M.Kind IN('Receipt','ReceiptClassification') AND M.Date<=$date;
            """,r=>(Id:r.GetString(0),Customer:r.GetString(1),Type:r.GetString(2),Code:r.GetString(3)),("$business",businessId),("$date",through.ToStorageDate()));
        var credits=CreditValues.List(db,businessId).Where(c=>c.IssueDate<=through).ToList();
        var ids=dues.Select(d=>d.CustomerId).Concat(payments.Select(p=>p.Customer)).Concat(credits.Select(c=>c.CustomerId)).Distinct();
        return ids.Select(id=>
        {
            var open=dues.Where(d=>d.CustomerId==id).ToList();
            string name=CustomerBook.Find(db,businessId,id)?.FullName??"Customer unavailable";
            long unused=payments.Where(p=>p.Customer==id && p.Type=="Income").Sum(p=>ValueAvailability.Payment(db,businessId,p.Id,through).Cents);
            long held=payments.Where(p=>p.Customer==id && p.Code=="2200").Sum(p=>ValueAvailability.Payment(db,businessId,p.Id,through).Cents);
            long credit=credits.Where(c=>c.CustomerId==id).Sum(c=>CreditValues.Available(db,businessId,c.Id,through).Cents);
            return new CustomerPosition(id,name,open.Sum(d=>d.Cents),open.Where(d=>d.DueDate<through).Sum(d=>d.Cents),
                open.Where(d=>d.DueDate==through).Sum(d=>d.Cents),open.Where(d=>d.DueDate>through).Sum(d=>d.Cents),
                open.Where(d=>d.DueDate is null).Sum(d=>d.Cents),unused,credit){HeldReceiptsCents=held};
        }).OrderBy(c=>c.Name).ThenBy(c=>c.CustomerId).ToList();
    }
}
