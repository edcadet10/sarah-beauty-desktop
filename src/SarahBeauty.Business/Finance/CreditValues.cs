using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

internal static class CreditValues
{
    internal static CustomerCredit Get(DbWork db,OwnerSession owner,string id)=>List(db,owner,id).SingleOrDefault()
        ??throw new ArgumentException("Choose a credit from this business.");

    internal static List<CustomerCredit> List(DbWork db,OwnerSession owner,string? id=null)=>db.Read("""
        SELECT C.Id,C.CustomerId,json_extract(P.DataJson,'$.FullName'),C.PaymentId,C.IssueDate,C.IssuedCents,C.IssuedTaxCents,C.Reason
        FROM CustomerCredits C JOIN Customers P ON P.Id=C.CustomerId WHERE C.BusinessId=$business AND ($id IS NULL OR C.Id=$id) ORDER BY C.IssueDate,C.Id;
        """,r=>new CustomerCredit(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),DateOnly.Parse(r.GetString(4)),r.GetInt64(5),r.GetInt64(6),r.GetString(7)),("$business",owner.BusinessId),("$id",id));

    internal static ValueBalance Available(DbWork db,OwnerSession owner,string id,DateOnly date)
    {
        _=Get(db,owner,id);
        var funded=ValueAvailability.Funding(db,"CreditId",id,date);
        var used=ValueAvailability.ApplicationSum(db,"CreditId",id,date);
        return new(checked(funded.Cents-used.Cents),checked(funded.TaxCents-used.TaxCents));
    }

    internal static void ValidateHistory(DbWork db,OwnerSession owner,string id)
    {
        var dates=db.Read("SELECT Date FROM CreditFunding WHERE CreditId=$id UNION SELECT Date FROM PaymentApplications WHERE CreditId=$id ORDER BY Date;",
            r=>DateOnly.Parse(r.GetString(0)),("$id",id));
        foreach(var date in dates)ValueAvailability.Validate(Available(db,owner,id,date),"Customer credit availability",date);
    }

    internal static string Fund(DbWork db,OwnerSession owner,string creditId,string paymentId,string kind,DateOnly date,
        long cents,long tax,string reason,string sourceDraftId,string? refundId=null,string? reversesFundingId=null)
    {
        string id=Guid.NewGuid().ToString();
        db.Execute("""
            INSERT INTO CreditFunding (Id,CreditId,PaymentId,Kind,Date,Cents,TaxCents,Reason,OwnerId,SourceDraftId,RefundMovementId,ReversesFundingId)
            VALUES ($id,$credit,$payment,$kind,$date,$cents,$tax,$reason,$owner,$draft,$refund,$reverses);
            """,("$id",id),("$credit",creditId),("$payment",paymentId),("$kind",kind),("$date",date.ToString("yyyy-MM-dd")),
            ("$cents",cents),("$tax",tax),("$reason",reason),("$owner",owner.OwnerId),("$draft",sourceDraftId),("$refund",refundId),("$reverses",reversesFundingId));
        return id;
    }
}
