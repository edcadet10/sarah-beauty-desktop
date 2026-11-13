using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsRequestPacingTests
{
    private sealed class Clock:TimeProvider
    {
        internal long Ticks;
        public override long TimestampFrequency=>TimeSpan.TicksPerSecond;
        public override long GetTimestamp()=>Ticks;
        internal void Advance(TimeSpan duration)=>Ticks+=duration.Ticks;
    }
    [Fact]
    public async Task SixtyFiveCallsAtZeroInitialTimestampRemainSpacedAcrossMinuteBoundaries()
    {
        var clock=new Clock();var pacing=new GoogleSheetsRequestPacing(clock){Delay=(duration,token)=>{token.ThrowIfCancellationRequested();clock.Advance(duration);return Task.CompletedTask;}};var starts=new List<long>();
        for(int i=0;i<65;i++){await pacing.WaitAsync();starts.Add(clock.Ticks);}
        Assert.Equal(0,starts[0]);for(int i=1;i<starts.Count;i++)Assert.True(starts[i]-starts[i-1]>=GoogleSheetsRequestPacing.Interval.Ticks);
        Assert.All(starts,start=>Assert.True(starts.Count(value=>value>=start&&value<start+TimeSpan.FromMinutes(1).Ticks)<=55));
        Assert.Same(GoogleSheetsRequestPacing.ForAccount("fictional-test-account"),GoogleSheetsRequestPacing.ForAccount("fictional-test-account"));
    }
    [Fact]
    public async Task EarlyTimerWakeDoesNotReleaseTheNextRequestTooSoon()
    {
        var clock=new Clock();int delays=0;var pacing=new GoogleSheetsRequestPacing(clock){Delay=(duration,token)=>{token.ThrowIfCancellationRequested();clock.Advance(++delays==1?duration/2:duration);return Task.CompletedTask;}};
        await pacing.WaitAsync();await pacing.WaitAsync();Assert.Equal(2,delays);Assert.Equal(GoogleSheetsRequestPacing.Interval.Ticks,clock.Ticks);
    }
    [Fact]
    public async Task CancellationDuringDelayReleasesTheGateWithoutConsumingASlot()
    {
        var clock=new Clock();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var pacing=new GoogleSheetsRequestPacing(clock);
        pacing.Delay=async(_,token)=>{entered.TrySetResult();await Task.Delay(Timeout.InfiniteTimeSpan,token);};await pacing.WaitAsync();using var cancellation=new CancellationTokenSource();
        var waiting=pacing.WaitAsync(cancellation.Token);await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>waiting);
        Assert.Equal(0,clock.Ticks);pacing.Delay=(duration,token)=>{clock.Advance(duration);return Task.CompletedTask;};await pacing.WaitAsync().WaitAsync(TimeSpan.FromSeconds(2));Assert.Equal(GoogleSheetsRequestPacing.Interval.Ticks,clock.Ticks);
    }
}
