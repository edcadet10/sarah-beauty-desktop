using System.Collections.Concurrent;

namespace SarahBeauty.Business.Google;

internal sealed class GoogleSheetsRequestPacing
{
    private static readonly ConcurrentDictionary<string,GoogleSheetsRequestPacing> Accounts=new(StringComparer.Ordinal);
    internal static readonly TimeSpan Interval=TimeSpan.FromMilliseconds(1100);
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly TimeProvider _time;
    private long? _last;
    internal Func<TimeSpan,CancellationToken,Task> Delay {get;set;}
    internal GoogleSheetsRequestPacing(TimeProvider? time=null){_time=time??TimeProvider.System;Delay=(duration,cancellation)=>Task.Delay(duration,_time,cancellation);}
    internal static GoogleSheetsRequestPacing ForAccount(string account)
    {
        GoogleDriveFolders.RequireId(account);return Accounts.GetOrAdd(account,static _=>new());
    }
    internal async Task WaitAsync(CancellationToken cancellation=default)
    {
        await _gate.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            while(_last is long last)
            {
                cancellation.ThrowIfCancellationRequested();var remaining=Interval-_time.GetElapsedTime(last,_time.GetTimestamp());if(remaining<=TimeSpan.Zero)break;
                await Delay(remaining,cancellation).ConfigureAwait(false);
            }
            cancellation.ThrowIfCancellationRequested();_last=_time.GetTimestamp();
        }
        finally{_gate.Release();}
    }
}
