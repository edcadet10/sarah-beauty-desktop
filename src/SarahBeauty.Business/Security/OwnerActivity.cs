namespace SarahBeauty.Business.Security;

public sealed class OwnerActivity(OwnerAccess access,OwnerSession session,TimeProvider? time=null)
{
    private readonly TimeProvider _time=time??TimeProvider.System;
    private readonly object _gate=new();
    private long? _lastRecorded;
    public bool Check()=>access.IsSessionCurrent(session);
    public bool RecordInput()
    {
        lock(_gate)
        {
            if(!session.IsActive)return false;long now=_time.GetTimestamp();
            if(_lastRecorded is long last&&_time.GetElapsedTime(last,now)<TimeSpan.FromSeconds(1))return true;
            if(!access.TryRecordActivity(session))return false;_lastRecorded=now;return true;
        }
    }
}
