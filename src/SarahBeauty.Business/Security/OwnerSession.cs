namespace SarahBeauty.Business.Security;

public sealed class OwnerSession
{
    public string OwnerId { get; }
    public string OwnerName { get; }
    public string BusinessId { get; }
    public string StoreId { get; }
    public string InstanceId {get;}
    internal string? CredentialStamp {get;}
    private readonly object _gate=new();
    private readonly TimeProvider _time;
    private static readonly TimeSpan IdleLimit=TimeSpan.FromMinutes(15);
    private long _lastActivity;
    private DateTimeOffset _expiresAt;
    private bool _locked;
    internal DateTimeOffset ExpiresAt { get{lock(_gate){Active();return _expiresAt;}} }
    public bool IsActive {get{lock(_gate)return Active();}}

    internal OwnerSession(string ownerId, string name, string businessId, string storeId,string instanceId,TimeProvider? time=null,string? credentialStamp=null)
    {
        (OwnerId, OwnerName, BusinessId, StoreId,InstanceId) = (ownerId, name, businessId, storeId,instanceId);
        CredentialStamp=credentialStamp;
        _time=time??TimeProvider.System;_lastActivity=_time.GetTimestamp();_expiresAt=_time.GetUtcNow()+IdleLimit;
    }

    internal void Touch()
    {
        lock(_gate)
        {
            if(!Active())throw new UnauthorizedAccessException("Sign in to this business to continue.");
            _lastActivity=_time.GetTimestamp();_expiresAt=_time.GetUtcNow()+IdleLimit;
        }
    }
    public void Lock(){lock(_gate){_locked=true;_expiresAt=DateTimeOffset.MinValue;}}
    private bool Active()
    {
        if(!_locked&&_time.GetElapsedTime(_lastActivity,_time.GetTimestamp())>=IdleLimit){_locked=true;_expiresAt=DateTimeOffset.MinValue;}
        return !_locked;
    }
}
