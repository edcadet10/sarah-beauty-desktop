using SarahBeauty.Business.Google;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop;

internal sealed class GoogleArchiveRuntime
{
    private readonly CancellationTokenSource _cancellation=new();
    private readonly Task _work;
    private string _status="Starting";
    private int _stopRequested;
    internal string Status=>Volatile.Read(ref _status);
    internal GoogleArchiveRuntime(BusinessStore store)=>_work=Task.Run(()=>Run(store));
    internal Task StopAsync(){if(Interlocked.Exchange(ref _stopRequested,1)==0)_cancellation.Cancel();return _work;}
    private async Task Run(BusinessStore store)
    {
        try
        {
            while(!_cancellation.IsCancellationRequested)
            {
                try
                {
                    Volatile.Write(ref _status,"Checking saved Drive permission and completed archives");var result=await new GoogleArchiveQueue(store).RunDueAsync(_cancellation.Token);
                    Volatile.Write(ref _status,result.Status+(result.State is null?"":$" • Observed watermark {result.State.ObservedWatermark} • Delivered {result.State.DeliveredWatermark?.ToString()??"None"}"));
                }
                catch(OperationCanceledException) when(_cancellation.IsCancellationRequested){break;}
                catch(Exception){Volatile.Write(ref _status,"Needs review. Refresh Google archive health and review delivery history.");}
                await Task.Delay(TimeSpan.FromSeconds(15),_cancellation.Token);
            }
        }
        catch(OperationCanceledException) when(_cancellation.IsCancellationRequested){}
        finally{Volatile.Write(ref _status,"Stopped");_cancellation.Dispose();}
    }
}
