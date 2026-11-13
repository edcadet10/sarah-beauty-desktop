using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

public sealed record ActivatedTestStore(BusinessStore Store,OwnerSession Owner,ActiveStoreSelection Selection);
public sealed class ActiveTestStores(string parent)
{
    private readonly ActiveBusinessStores _stores=new(parent,BusinessMode.Test);
    internal Action<string>? FailurePoint {get=>_stores.FailurePoint;set=>_stores.FailurePoint=value;}
    public string ResolveDatabase()=>_stores.ResolveDatabase();
    public async Task<ActivatedTestStore> ActivateAsync(string requestId,string ownerId,string passphrase,CancellationToken cancellation=default)
    {
        var result=await _stores.ActivateAsync(requestId,ownerId,passphrase,cancellation);return new(result.Store,result.Owner,result.Selection);
    }
}
