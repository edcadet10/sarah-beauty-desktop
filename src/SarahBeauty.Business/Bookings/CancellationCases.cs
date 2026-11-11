using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Bookings;

public sealed class CancellationCases(BusinessStore store)
{
    public CancellationPreview Preview(OwnerSession owner,CancellationDraft draft)=>store.Transaction(db=>
    {
        store.RequireSession(db,owner);
        if(draft.Date is null)throw new ArgumentException("Choose the cancellation decision date.");
        var before=new CancellationSources(store).Load(db,owner,draft.BookingId,draft.Date.Value);
        db.Execute("SAVEPOINT CancellationPreview;");
        try{return new CancellationExecution(store,owner,db,draft).Run(before);}
        finally{db.Execute("ROLLBACK TO CancellationPreview; RELEASE CancellationPreview;");}
    });
}
