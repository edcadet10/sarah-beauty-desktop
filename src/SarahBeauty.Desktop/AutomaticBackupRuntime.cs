using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop;

internal sealed class AutomaticBackupRuntime
{
    private readonly CancellationTokenSource _cancellation=new();
    private readonly Task _work;
    private string _status="Starting";
    private int _stopRequested;
    internal string Status=>Volatile.Read(ref _status);
    internal AutomaticBackupRuntime(BusinessStore store)=>_work=Task.Run(()=>Run(store));
    internal Task StopAsync()
    {
        if(Interlocked.Exchange(ref _stopRequested,1)==0)_cancellation.Cancel();return _work;
    }
    private async Task Run(BusinessStore store)
    {
        try
        {
            while(!_cancellation.IsCancellationRequested)
            {
                try
                {
                    Volatile.Write(ref _status,"Checking saved permission and pending work");var result=await new AutomaticBackups(store).RunDueAsync(_cancellation.Token);
                    Volatile.Write(ref _status,result.Status+(result.State is null?"":$" • Pending watermark {result.State.PendingWatermark} • Archived {result.State.ArchivedWatermark?.ToString()??"None"}"));
                }
                catch(OperationCanceledException) when(_cancellation.IsCancellationRequested){break;}
                catch(Exception){Volatile.Write(ref _status,"Needs review. Refresh backup health and review the latest attempt.");}
                await Task.Delay(TimeSpan.FromSeconds(15),_cancellation.Token);
            }
        }
        catch(OperationCanceledException) when(_cancellation.IsCancellationRequested){}
        finally{Volatile.Write(ref _status,"Stopped");_cancellation.Dispose();}
    }
}
