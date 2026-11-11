namespace SarahBeauty.Business.Security;

public sealed class OwnerSession
{
    public string OwnerId { get; }
    public string OwnerName { get; }
    public string BusinessId { get; }
    public string StoreId { get; }
    public string InstanceId {get;}
    internal DateTimeOffset ExpiresAt { get; private set; }

    internal OwnerSession(string ownerId, string name, string businessId, string storeId,string instanceId)
    {
        (OwnerId, OwnerName, BusinessId, StoreId,InstanceId) = (ownerId, name, businessId, storeId,instanceId);
        Touch();
    }

    internal void Touch() => ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
    public void Lock() => ExpiresAt = DateTimeOffset.MinValue;
}
