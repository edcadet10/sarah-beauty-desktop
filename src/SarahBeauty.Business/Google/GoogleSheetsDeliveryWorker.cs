using System.Collections.Concurrent;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed partial class GoogleSheetsDeliveryWorker
{
    private static readonly ConcurrentDictionary<string,byte> ActiveAttempts=new();
    internal static bool IsActive(string id)=>ActiveAttempts.ContainsKey(id);
    private readonly BusinessStore _store;
    private readonly GoogleConnections _connections;
    private readonly GoogleOutputSettings _settings;
    private readonly GoogleSheetsDeliveries _deliveries;
    private readonly HttpClient _client;
    internal Action<string>? FailurePoint {get;set;}
    internal Func<CancellationToken,Task>? WaitForRequest {get;set;}
    public GoogleSheetsDeliveryWorker(BusinessStore store):this(store,new(store),new(store),GoogleApiJson.Client){}
    internal GoogleSheetsDeliveryWorker(BusinessStore store,GoogleConnections connections,GoogleOutputSettings settings,HttpClient client){_store=store;_connections=connections;_settings=settings;_deliveries=new(store);_client=client;}

    public Task<GoogleSheetsAttempt> ExportAsync(OwnerSession owner,ReadableExportSelection selection,CancellationToken cancellation=default)
    {
        _store.Read(db=>{_store.RequireSession(db,owner);return true;});return ExportCurrentAsync(selection,cancellation);
    }
    public Task<GoogleSheetsAttempt> RetryAsync(OwnerSession owner,string jobId,CancellationToken cancellation=default)
    {
        _store.Read(db=>{_store.RequireSession(db,owner);return true;});return RetryCurrentAsync(jobId,cancellation);
    }
    internal Task<GoogleSheetsAttempt> ExportCurrentAsync(ReadableExportSelection selection,CancellationToken cancellation=default)
    {
        ArgumentNullException.ThrowIfNull(selection);selection.Validate();return RunAsync(selection,null,cancellation);
    }
    internal Task<GoogleSheetsAttempt> RetryCurrentAsync(string jobId,CancellationToken cancellation=default)=>RunAsync(null,jobId,cancellation);

    private async Task<GoogleSheetsAttempt> RunAsync(ReadableExportSelection? selection,string? jobId,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();using var lease=GoogleSheetsDeliveryLease.TryAcquire(_store)??throw new GoogleConnectionFailure("GoogleSheetsBusy","Another readable delivery is active. Retry after it finishes.");
        var permit=_settings.Authorize(GoogleOutputKind.SheetsExport);_deliveries.InterruptRunning();var job=selection is null?Existing(permit,jobId!):Capture(permit,selection,cancellation);
        var attempt=_deliveries.Begin(permit,job,cancellation);ActiveAttempts.TryAdd(attempt.Id,0);
        try
        {
            var generation=new ReadableGenerationFiles(_store).Load(GoogleSheetsJobRecords.Artifact(job),cancellation);GoogleSheetsExportJobs.RequireGeneration(job,generation);
            var access=new GoogleSheetsDeliveryAccess(_store,_connections,permit,job,attempt,_client);if(WaitForRequest is not null)access.Wait=WaitForRequest;
            var result=await DeliverContentAsync(permit,job,attempt,generation,access,cancellation).ConfigureAwait(false);FailurePoint?.Invoke("Remote readable export verified");
            cancellation.ThrowIfCancellationRequested();return _deliveries.Complete(permit,job,attempt,result,cancellation);
        }
        catch(Exception error){RecordFailure(permit,attempt,error);throw;}
        finally{ActiveAttempts.TryRemove(attempt.Id,out _);}
    }
    private GoogleSheetsExportJob Capture(GoogleOutputPermit permit,ReadableExportSelection selection,CancellationToken cancellation)
    {
        using var snapshot=new ReadableExportSnapshots(_store).Capture(permit,cancellation);var generation=ReadableExportGeneration.Build(snapshot,selection,cancellation);
        var artifact=new ReadableGenerationFiles(_store).Stage(generation,cancellation);return new GoogleSheetsExportJobs(_store).Reserve(permit,artifact,cancellation);
    }
    private GoogleSheetsExportJob Existing(GoogleOutputPermit permit,string id)=>_store.Read(db=>
    {
        var job=GoogleSheetsJobRecords.Get(db,permit.Policy.BusinessId,id);new GoogleSheetsExportJobs(_store).Require(db,permit,job);return job;
    });
    private void RecordFailure(GoogleOutputPermit permit,GoogleSheetsAttempt attempt,Exception error)
    {
        Exception failure=error;if(error is GoogleConnectionFailure google)try{_connections.ObserveFailure(permit.Connection,google);}catch(GoogleConnectionFailure observation){failure=observation;}
        try{_deliveries.Fail(attempt,failure);}
        catch(Exception recordError) when(recordError is IOException or InvalidDataException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {throw new GoogleConnectionFailure("GoogleDeliveryStateUnconfirmed","The readable attempt ended, but its local outcome could not be saved. Preserve its files and review history before retrying.");}
        if(!ReferenceEquals(failure,error))throw failure;
    }
}
