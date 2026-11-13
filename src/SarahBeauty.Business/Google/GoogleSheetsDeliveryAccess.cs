using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

internal sealed class GoogleSheetsDeliveryAccess(BusinessStore store,GoogleConnections connections,GoogleOutputPermit permit,GoogleSheetsExportJob job,GoogleSheetsAttempt attempt,HttpClient client)
{
    internal Func<CancellationToken,Task> Wait {get;set;}=GoogleSheetsRequestPacing.ForAccount(permit.Connection.Account.Account.PermissionId).WaitAsync;
    internal async Task<string> TokenAsync(CancellationToken cancellation)
    {
        await Wait(cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();Require();var grant=await connections.AccessAsync(cancellation).ConfigureAwait(false);
        if(grant.Connection!=permit.Connection)throw new GoogleConnectionFailure("GoogleOutputConnectionChanged","The Google connection changed during readable delivery. Review the destination.");
        cancellation.ThrowIfCancellationRequested();Require();connections.RequireAccess(grant);return grant.AccessToken;
    }
    internal async Task PrivateAsync(string state,CancellationToken cancellation)
    {
        await FolderAsync(cancellation).ConfigureAwait(false);
        await new GoogleSheetsFiles(client).VerifyAsync(GoogleSheetsJobRecords.Spec(job),FileId(),permit.Connection.Account.Account.PermissionId,TokenAsync,cancellation,state).ConfigureAwait(false);Require();
    }
    internal async Task FolderAsync(CancellationToken cancellation)
    {
        await new GoogleDriveFolders(client).VerifyAsync(await TokenAsync(cancellation).ConfigureAwait(false),job.FolderId,permit.Connection.Account.Account.PermissionId,job.FolderBinding,cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();Require();
    }
    internal void Require()=>store.Read(db=>{new GoogleSheetsDeliveries(store).RequireRunning(db,permit,job,attempt);return true;});
    private string FileId()=>store.Read(db=>
    {
        new GoogleSheetsDeliveries(store).RequireRunning(db,permit,job,attempt);var creation=GoogleSheetsCreationRecords.Get(db,job);
        return creation is{State:"FileKnown",FileId:not null}?creation.FileId:throw GoogleSheetsCreationRecords.Invalid();
    });
}
