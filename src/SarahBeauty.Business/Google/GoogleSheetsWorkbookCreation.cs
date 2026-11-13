using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

internal sealed class GoogleSheetsWorkbookCreation(BusinessStore store,HttpClient? client=null)
{
    internal async Task<GoogleSheetsFileObservation> ResolveAsync(GoogleOutputPermit permit,GoogleSheetsExportJob job,Func<CancellationToken,Task<string>> access,CancellationToken cancellation=default)
    {
        var checkpoints=new GoogleSheetsCreationCheckpoints(store);var jobs=new GoogleSheetsExportJobs(store);var files=new GoogleSheetsFiles(client);
        var spec=GoogleSheetsJobRecords.Spec(job);string account=permit.Connection.Account.Account.PermissionId;
        async Task<string> CurrentAccess(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();Require();string value=await access(token).ConfigureAwait(false);token.ThrowIfCancellationRequested();Require();return value;
        }
        void Require()=>store.Read(db=>{jobs.Require(db,permit,job);return true;});
        async Task Folder()=>await new GoogleDriveFolders(client).VerifyAsync(await CurrentAccess(cancellation).ConfigureAwait(false),job.FolderId,account,job.FolderBinding,cancellation).ConfigureAwait(false);

        var checkpoint=checkpoints.Prepare(permit,job,cancellation);await Folder().ConfigureAwait(false);
        if(checkpoint.State=="Prepared")
        {
            checkpoint=checkpoints.BeginCreate(permit,job,checkpoint,cancellation);
            string id=await files.CreateEmptyAsync(spec,CurrentAccess,cancellation).ConfigureAwait(false);
            checkpoint=checkpoints.RecordFile(job,checkpoint,id);
        }
        else if(checkpoint.State=="CreateUncertain")
        {
            var found=await files.FindAsync(spec,account,CurrentAccess,cancellation).ConfigureAwait(false)??throw new GoogleConnectionFailure("ReadableWorkbookCreationUnconfirmed","Workbook creation is uncertain and no complete job match was found. Review this job before creating another export.");
            checkpoint=checkpoints.RecordFile(job,checkpoint,found.FileId);
        }
        var observed=await files.VerifyAsync(spec,checkpoint.FileId!,account,CurrentAccess,cancellation,requiredState:null).ConfigureAwait(false);
        await Folder().ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();Require();return observed;
    }
}
