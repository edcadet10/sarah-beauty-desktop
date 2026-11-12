using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

internal sealed class GoogleSheetsCreationCheckpoints(BusinessStore store)
{
    internal GoogleSheetsCreation Prepare(GoogleOutputPermit permit,GoogleSheetsExportJob job,CancellationToken cancellation=default)=>store.Transaction(db=>
    {
        cancellation.ThrowIfCancellationRequested();new GoogleSheetsExportJobs(store).Require(db,permit,job);var existing=GoogleSheetsCreationRecords.Get(db,job);
        if(existing is not null)return existing;var value=new GoogleSheetsCreation(1,job.Id,job.BusinessId,job.InstanceId,1,"Prepared",null,DateTimeOffset.UtcNow.ToString("O"));
        GoogleSheetsCreationRecords.Insert(db,job,value);cancellation.ThrowIfCancellationRequested();return value;
    });

    internal GoogleSheetsCreation BeginCreate(GoogleOutputPermit permit,GoogleSheetsExportJob job,GoogleSheetsCreation expected,CancellationToken cancellation=default)=>store.Transaction(db=>
    {
        cancellation.ThrowIfCancellationRequested();new GoogleSheetsExportJobs(store).Require(db,permit,job);
        if(expected.State!="Prepared")throw GoogleSheetsCreationRecords.Invalid();
        var next=expected with{Revision=2,State="CreateUncertain",UpdatedAtUtc=DateTimeOffset.UtcNow.ToString("O")};
        GoogleSheetsCreationRecords.Replace(db,job,expected,next);cancellation.ThrowIfCancellationRequested();return next;
    });

    // Preserve an already-returned remote identity even if output permission was withdrawn during HTTP.
    // Recording this fact grants no permission to read or mutate the workbook.
    internal GoogleSheetsCreation RecordFile(GoogleSheetsExportJob job,GoogleSheetsCreation expected,string fileId)=>store.Transaction(db=>
    {
        GoogleDriveFolders.RequireId(fileId);RequireStored(db,job);var current=GoogleSheetsCreationRecords.Get(db,job);
        if(expected.State!="CreateUncertain"||expected.JobId!=job.Id||expected.BusinessId!=job.BusinessId||expected.InstanceId!=job.InstanceId)throw GoogleSheetsCreationRecords.Invalid();
        if(current is{State:"FileKnown"}&&current.FileId==fileId)return current;
        var next=expected with{Revision=3,State="FileKnown",FileId=fileId,UpdatedAtUtc=DateTimeOffset.UtcNow.ToString("O")};
        GoogleSheetsCreationRecords.Replace(db,job,expected,next);return next;
    });

    internal GoogleSheetsCreation? Current(GoogleSheetsExportJob job)=>store.Read(db=>{RequireStored(db,job);return GoogleSheetsCreationRecords.Get(db,job);});
    private void RequireStored(DbWork db,GoogleSheetsExportJob job)
    {
        if(job.StoreId!=store.StoreId||job.InstanceId!=store.InstanceId||GoogleSheetsJobRecords.Get(db,job.BusinessId,job.Id)!=job)throw GoogleSheetsCreationRecords.Invalid();
    }
}
