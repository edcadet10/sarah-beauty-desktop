namespace SarahBeauty.Business.Security;

public sealed class OwnerSession
{
    public string OwnerId { get; }
    public string OwnerName { get; }
    public string BusinessId { get; }
    public string StoreId { get; }
    public string InstanceId {get;}
    private readonly object _gate=new();
    private DateTimeOffset _expiresAt;
    private bool _locked;
    internal DateTimeOffset ExpiresAt { get{lock(_gate)return _expiresAt;} }

    internal OwnerSession(string ownerId, string name, string businessId, string storeId,string instanceId)
    {
        (OwnerId, OwnerName, BusinessId, StoreId,InstanceId) = (ownerId, name, businessId, storeId,instanceId);
        _expiresAt=DateTimeOffset.UtcNow.AddMinutes(15);
    }

    internal void Touch()
    {
        lock(_gate)
        {
            if(_locked||_expiresAt<=DateTimeOffset.UtcNow){_locked=true;_expiresAt=DateTimeOffset.MinValue;throw new UnauthorizedAccessException("Sign in to this business to continue.");}
            _expiresAt=DateTimeOffset.UtcNow.AddMinutes(15);
        }
    }
    public void Lock(){lock(_gate){_locked=true;_expiresAt=DateTimeOffset.MinValue;}}
}
