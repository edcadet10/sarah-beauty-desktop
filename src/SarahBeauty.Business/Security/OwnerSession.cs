namespace SarahBeauty.Business.Security;

public sealed class OwnerSession
{
    public string OwnerId { get; }
    public string OwnerName { get; }
    public string BusinessId { get; }
    public string StoreId { get; }
    internal DateTimeOffset ExpiresAt { get; private set; }

    internal OwnerSession(string ownerId, string name, string businessId, string storeId)
    {
        (OwnerId, OwnerName, BusinessId, StoreId) = (ownerId, name, businessId, storeId);
        Touch();
    }

    internal void Touch() => ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
    public void Lock() => ExpiresAt = DateTimeOffset.MinValue;
}
