using SarahBeauty.Business.Security;

namespace SarahBeauty.Storage.Tests;

public sealed class OwnerIdleTimeTests
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
    public void ClockRollbackCannotExtendAnIdleSession()
    {
        var clock=new Clock();var session=Session(clock);clock.Advance(TimeSpan.FromMinutes(16));clock.Utc=clock.Utc.AddHours(-2);
        Assert.False(session.IsActive);Assert.Throws<UnauthorizedAccessException>(()=>session.Touch());
    }
    [Fact]
    public void ClockForwardDoesNotExpireAFreshSessionEarly()
    {
        var clock=new Clock();var session=Session(clock);clock.Advance(TimeSpan.FromMinutes(1));clock.Utc=clock.Utc.AddDays(1);Assert.True(session.IsActive);
    }
    [Fact]
    public void StatusPollingDoesNotRenewIdleAndExpiredAccessStaysExpiredAfterRollback()
    {
        var clock=new Clock();var session=Session(clock);var expiry=session.ExpiresAt;
        for(int minute=0;minute<14;minute++){clock.Advance(TimeSpan.FromMinutes(1));Assert.True(session.IsActive);Assert.Equal(expiry,session.ExpiresAt);}
        clock.Advance(TimeSpan.FromMinutes(1));Assert.False(session.IsActive);Assert.Equal(DateTimeOffset.MinValue,session.ExpiresAt);clock.Utc=clock.Utc.AddDays(-1);Assert.False(session.IsActive);Assert.Throws<UnauthorizedAccessException>(()=>session.Touch());
    }
    [Fact]
    public void ForegroundProtectedAccessRenewsTheElapsedIdleWindow()
    {
        using var f=new BusinessFixture();var clock=new Clock();var session=new OwnerSession(f.Owner.OwnerId,f.Owner.OwnerName,f.Owner.BusinessId,f.Store.StoreId,f.Store.InstanceId,clock);
        clock.Advance(TimeSpan.FromMinutes(14));Assert.NotEmpty(f.Store.BusinessName(session));clock.Advance(TimeSpan.FromMinutes(14));Assert.True(session.IsActive);
        clock.Advance(TimeSpan.FromMinutes(1));Assert.Throws<UnauthorizedAccessException>(()=>f.Store.BusinessName(session));Assert.False(session.IsActive);
    }
    [Fact]
    public void ProtectedReadsRejectElapsedIdleEvenWhenUtcMovesBack()
    {
        using var f=new BusinessFixture();var clock=new Clock();var session=new OwnerSession(f.Owner.OwnerId,f.Owner.OwnerName,f.Owner.BusinessId,f.Store.StoreId,f.Store.InstanceId,clock);
        clock.Advance(TimeSpan.FromMinutes(16));clock.Utc=clock.Utc.AddHours(-2);Assert.Throws<UnauthorizedAccessException>(()=>f.Store.BusinessName(session));
    }
    private static OwnerSession Session(Clock clock)=>new("fictional-owner","Fictional owner","fictional-business","fictional-store","fictional-instance",clock);
}
