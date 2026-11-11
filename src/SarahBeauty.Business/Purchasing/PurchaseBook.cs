using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Purchasing;

public sealed class PurchaseBook(BusinessStore store)
{
    private readonly RecordStore<Purchase> _records=new(store,"Purchase");
    internal Action<string>? FailurePoint {get;set;}
    public List<Purchase> List(OwnerSession owner)=>_records.List(owner);

    public Purchase SaveDraft(OwnerSession owner,Purchase value)
    {
        value=value with{Id=value.Id.Length==0?Guid.NewGuid().ToString():value.Id,Name=value.Name.Trim()};
        return store.Write(owner,"Save supplier bill draft",value.Id,db=>
        {
            if(value.Status!="Draft" || value.ApprovedBy.Length>0 || value.ApprovedAtUtc.Length>0
                || (value.Revision>0 && _records.Get(db,owner,value.Id).Status!="Draft"))
                throw new ArgumentException("Approved supplier bills retain their original lines. Use a linked adjustment.");
            return _records.Save(db,owner,value);
        });
    }

    public Purchase Preview(OwnerSession owner,Purchase value)=>store.Read(db=>
    {store.RequireSession(db,owner);PurchaseValidation.Validate(db,store,owner,value);return value;});

    public Purchase Approve(OwnerSession owner,string id,int revision,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);
            var value=_records.Get(db,owner,id);
            if(value.Status=="Approved")return value;
            if(value.Status!="Draft" || value.Revision!=revision)throw new ArgumentException("Reload the changed supplier bill draft.");
            PurchaseValidation.Validate(db,store,owner,value);
            var result=_records.Save(db,owner,value with{Status="Approved",ApprovedBy=owner.OwnerId,ApprovedAtUtc=DateTimeOffset.UtcNow.ToString("O")});
            FailurePoint?.Invoke("purchase-approval");
            BusinessStore.Audit(db,owner,"Approve supplier bill",id);
            return result;
        });
    }
}
