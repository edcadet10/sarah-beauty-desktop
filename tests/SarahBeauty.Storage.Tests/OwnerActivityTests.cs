using SarahBeauty.Business.Security;

namespace SarahBeauty.Storage.Tests;

public sealed class OwnerActivityTests
{
    private sealed class Clock:TimeProvider
    {
        internal DateTimeOffset Utc=DateTimeOffset.UtcNow;
        internal long Ticks;
        public override long TimestampFrequency=>TimeSpan.TicksPerSecond;
        public override long GetTimestamp()=>Ticks;
        public override DateTimeOffset GetUtcNow()=>Utc;
        internal void Advance(TimeSpan elapsed){Ticks+=elapsed.Ticks;Utc+=elapsed;}
    }
    [Fact]
    public void AuthoritativeChecksDoNotRenewIdleOrChangeBusinessWatermark()
    {
        using var f=new BusinessFixture();var clock=new Clock();var session=Session(f,clock);var activity=new OwnerActivity(new(f.Store),session,clock);var expiry=session.ExpiresAt;long watermark=Watermark(f);
        for(int minute=0;minute<14;minute++){clock.Advance(TimeSpan.FromMinutes(1));Assert.True(activity.Check());Assert.Equal(expiry,session.ExpiresAt);}
        clock.Advance(TimeSpan.FromMinutes(1));Assert.False(activity.Check());Assert.False(activity.RecordInput());Assert.Equal(DateTimeOffset.MinValue,session.ExpiresAt);Assert.Equal(watermark,Watermark(f));
    }
    [Fact]
    public void InputRenewsAtMostOncePerSecondAndKeepsTheSessionAliveForRealActivity()
    {
        using var f=new BusinessFixture();var clock=new Clock();var session=Session(f,clock);var activity=new OwnerActivity(new(f.Store),session,clock);Assert.True(activity.RecordInput());var expiry=session.ExpiresAt;
        clock.Advance(TimeSpan.FromMilliseconds(500));Assert.True(activity.RecordInput());Assert.Equal(expiry,session.ExpiresAt);clock.Advance(TimeSpan.FromMilliseconds(500));Assert.True(activity.RecordInput());Assert.Equal(expiry.AddSeconds(1),session.ExpiresAt);
        clock.Advance(TimeSpan.FromMinutes(14));Assert.True(activity.RecordInput());clock.Advance(TimeSpan.FromMinutes(14));Assert.True(activity.Check());clock.Advance(TimeSpan.FromMinutes(1));Assert.False(activity.Check());
    }
    [Fact]
    public void LockedForeignAndInactiveOwnersCannotRenewActivity()
    {
        using var f=new BusinessFixture();using var foreign=new BusinessFixture();var access=new OwnerAccess(f.Store);Assert.False(access.IsSessionCurrent(foreign.Owner));Assert.False(access.TryRecordActivity(foreign.Owner));
        f.Owner.Lock();Assert.False(access.TryRecordActivity(f.Owner));Assert.Equal(DateTimeOffset.MinValue,f.Owner.ExpiresAt);
        var fresh=access.SignIn(f.Owner.OwnerId,BusinessFixture.Phrase);f.Files.Execute($"UPDATE Owners SET Active=0 WHERE Id='{fresh.OwnerId}';");Assert.False(access.IsSessionCurrent(fresh));Assert.False(access.TryRecordActivity(fresh));
    }
    private static OwnerSession Session(BusinessFixture f,Clock clock)=>new(f.Owner.OwnerId,f.Owner.OwnerName,f.Owner.BusinessId,f.Store.StoreId,f.Store.InstanceId,clock,f.Owner.CredentialStamp);
    private static long Watermark(BusinessFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));
}
