using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Bookings;

public sealed class BookingFinancialSources(BusinessStore store)
{
    internal Action? AfterBookingRead {get;set;}
    public BookingFinancialSnapshot Load(OwnerSession owner,string bookingId,DateOnly from,DateOnly through)=>store.ReadSnapshot(db=>Load(db,owner,bookingId,from,through));

    internal BookingFinancialSnapshot Load(DbWork db,OwnerSession owner,string bookingId,DateOnly from,DateOnly through)
    {
        store.RequireSession(db,owner);if(from>through)throw new ArgumentException("The booking financial period ends before it starts.");
        var booking=new RecordStore<Booking>(store,"Booking").Get(db,owner,bookingId);AfterBookingRead?.Invoke();
        return Read(db,owner.BusinessId,booking,from,through,DateTimeOffset.UtcNow.ToString("O"));
    }

    internal static BookingFinancialSnapshot Read(DbWork db,string businessId,Booking booking,DateOnly from,DateOnly through,string generatedAtUtc)
    {
        if(from>through)throw new ArgumentException("The booking financial period ends before it starts.");
        return new((string)db.Scalar("SELECT DisplayName FROM Business WHERE Id=$id;",("$id",businessId))!,
            (string)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!,Convert.ToInt64(db.Scalar("SELECT Watermark FROM Business WHERE Id=$id;",("$id",businessId))),generatedAtUtc,
            from,through,booking.Id,booking.Name,booking.Revision,Entries(db,businessId,booking.Id,through),Cash(db,businessId,booking.Id,through));
    }

    internal static BookingFinancialEntry[] UnassignedCosts(DbWork db,OwnerSession owner,DateOnly through)=>UnassignedCosts(db,owner.BusinessId,through);
    internal static BookingFinancialEntry[] UnassignedCosts(DbWork db,string businessId,DateOnly through)=>Entries(db,businessId,null,through).Where(e=>e.IsDirectCost).ToArray();

    private static BookingFinancialEntry[] Entries(DbWork db,string businessId,string? booking,DateOnly through)=>db.Read("""
        SELECT M.Id,M.Date,M.Kind,CASE WHEN M.Kind='Reversal' THEN O.Kind ELSE M.Kind END,M.Memo,A.Code,A.Name,A.Type,J.DebitCents,J.CreditCents
        FROM Movements M JOIN JournalLines J ON J.MovementId=M.Id JOIN Accounts A ON A.Id=J.AccountId LEFT JOIN Movements O ON O.Id=M.ReversesId
        WHERE M.BusinessId=$business AND (M.BookingId=$booking OR ($booking IS NULL AND M.BookingId IS NULL AND A.Type='Expense'))
        AND M.Date<=$through ORDER BY M.Date,M.rowid,J.rowid;
        """,r=>new BookingFinancialEntry(r.GetString(0),DateOnly.Parse(r.GetString(1)),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetString(6),
            r.GetInt64(8),r.GetInt64(9),IsDirectCost(r.GetString(7),r.GetString(5)),
            r.GetString(7)=="Income" && r.GetString(5)!="4010" && r.GetString(3) is "Receipt" or "ReceiptClassification" or "Refund"),
        ("$business",businessId),("$booking",booking),("$through",through.ToString("yyyy-MM-dd"))).ToArray();

    internal static bool IsDirectCost(string accountType,string code)=>accountType=="Expense" && code is not ("5030" or "5035" or "5040" or "5060");

    private static BookingCashSource[] Cash(DbWork db,string businessId,string booking,DateOnly through)=>db.Read("""
        SELECT M.Id,M.Date,M.Kind,CASE WHEN M.Kind='Reversal' THEN O.Kind ELSE M.Kind END,M.Memo,
            json_extract(CASE WHEN M.Kind='Reversal' THEN O.DataJson ELSE M.DataJson END,'$.CashAccountId'),
            CASE WHEN M.Kind='Reversal' THEN O.GrossCents ELSE M.GrossCents END,
            CASE WHEN M.Kind='Reversal' THEN O.TaxCents ELSE M.TaxCents END,
            COALESCE(json_extract(CASE WHEN M.Kind='Reversal' THEN O.DataJson ELSE M.DataJson END,'$.FeeCents'),0)
        FROM Movements M LEFT JOIN Movements O ON O.Id=M.ReversesId
        WHERE M.BusinessId=$business AND M.BookingId=$booking AND M.Date<=$through
        AND CASE WHEN M.Kind='Reversal' THEN O.Kind ELSE M.Kind END IN('Receipt','UnclassifiedReceipt','Refund') ORDER BY M.Date,M.rowid;
        """,r=>CashRow(r.GetString(0),DateOnly.Parse(r.GetString(1)),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetInt64(6),r.GetInt64(7),r.GetInt64(8)),
        ("$business",businessId),("$booking",booking),("$through",through.ToString("yyyy-MM-dd"))).ToArray();

    private static BookingCashSource CashRow(string id,DateOnly date,string kind,string effectiveKind,string memo,string account,long gross,long tax,long fee)
    {
        int sign=(effectiveKind=="Refund"?-1:1)*(kind=="Reversal"?-1:1);
        return new(id,date,kind,effectiveKind,memo,account,checked(sign*gross),checked(sign*tax),checked(sign*fee));
    }
}
