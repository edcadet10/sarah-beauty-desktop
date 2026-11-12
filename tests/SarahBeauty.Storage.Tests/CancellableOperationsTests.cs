using SarahBeauty.Business.Runtime;

namespace SarahBeauty.Storage.Tests;

public sealed class CancellableOperationsTests
{
    [Fact]
    public async Task ClosingCancelsRegisteredWorkWaitsForCleanupAndRejectsNewWork()
    {
        var operations=new CancellableOperations();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var cancelled=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var cleanup=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work=operations.RunAsync(async cancellation=>
        {
            using var registration=cancellation.Register(()=>cancelled.TrySetResult());entered.SetResult();try{await Task.Delay(Timeout.Infinite,cancellation);return true;}finally{await cleanup.Task;}
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));var stop=operations.StopAsync();await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.False(stop.IsCompleted);
        cleanup.SetResult();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>work);await stop.WaitAsync(TimeSpan.FromSeconds(5));bool called=false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>operations.RunAsync(_=>{called=true;return Task.FromResult(true);}));Assert.False(called);await operations.StopAsync();
    }
    [Fact]
    public async Task AFailedOperationDoesNotLeaveShutdownWaitingOrMaskItsFailure()
    {
        var operations=new CancellableOperations();await Assert.ThrowsAsync<IOException>(()=>operations.RunAsync<bool>(_=>throw new IOException("Fictional operation failure")));await operations.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }
    [Fact]
    public async Task CallerCancellationDoesNotCancelAnotherRegisteredOperation()
    {
        var operations=new CancellableOperations();using var caller=new CancellationTokenSource();var otherRelease=new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled=operations.RunAsync(async cancellation=>{await Task.Delay(Timeout.Infinite,cancellation);return 0;},caller.Token);var other=operations.RunAsync(_=>otherRelease.Task);
        caller.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>cancelled);Assert.False(other.IsCompleted);otherRelease.SetResult(42);Assert.Equal(42,await other);await operations.StopAsync();
    }
}
