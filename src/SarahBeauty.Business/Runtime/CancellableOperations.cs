namespace SarahBeauty.Business.Runtime;

public sealed class CancellableOperations
{
    private readonly object _gate=new();
    private readonly CancellationTokenSource _shutdown=new();
    private readonly HashSet<Task> _pending=[];
    private bool _stopping;
    public async Task<T> RunAsync<T>(Func<CancellationToken,Task<T>> action,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();var completed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);CancellationTokenSource linked;
        lock(_gate)
        {
            if(_stopping)throw new OperationCanceledException("The application is closing.");linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,_shutdown.Token);_pending.Add(completed.Task);
        }
        using(linked)
        {
            try{linked.Token.ThrowIfCancellationRequested();return await action(linked.Token).ConfigureAwait(false);}
            finally{lock(_gate){_pending.Remove(completed.Task);completed.TrySetResult();}}
        }
    }
    public async Task StopAsync()
    {
        Task[] pending;bool cancel;
        lock(_gate){cancel=!_stopping;_stopping=true;pending=_pending.ToArray();}
        if(cancel){try{_shutdown.Cancel();}catch(AggregateException){}}
        await Task.WhenAll(pending).ConfigureAwait(false);
    }
}
