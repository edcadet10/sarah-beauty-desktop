using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

public sealed class GoogleSheetsExportProfiles(BusinessStore store)
{
    public GoogleSheetsExportProfile? Current(OwnerSession owner)=>store.Read(db=>{store.RequireSession(db,owner);return GoogleSheetsProfileRecords.Read(db,owner.BusinessId,store.InstanceId);});
    public GoogleSheetsExportProfile Save(OwnerSession owner,GoogleSheetsProfileDraft draft,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();GoogleSheetsProfileRecords.ValidateSelection(draft.Selection,draft.MinimumIntervalMinutes);
        return store.Transaction(db=>
        {
            cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);var prior=GoogleSheetsProfileRecords.Read(db,owner.BusinessId,store.InstanceId);
            if(draft.ExpectedRevision!=(prior?.Revision??0))throw new ArgumentException("Readable export settings changed. Reload them before saving.");
            if(prior is not null&&prior.Selection==draft.Selection&&prior.AutomaticEnabled==draft.AutomaticEnabled&&prior.MinimumIntervalMinutes==draft.MinimumIntervalMinutes)return prior;
            var value=new GoogleSheetsExportProfile(1,owner.BusinessId,store.InstanceId,checked(draft.ExpectedRevision+1),draft.Selection,draft.AutomaticEnabled,draft.MinimumIntervalMinutes,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));
            GoogleSheetsProfileRecords.Save(db,value);BusinessStore.Audit(db,owner,"Changed readable export report dates and schedule",null);cancellation.ThrowIfCancellationRequested();return value;
        });
    }
    internal void Require(DbWork db,GoogleSheetsExportProfile profile)
    {
        if(profile.InstanceId!=store.InstanceId||!profile.AutomaticEnabled||GoogleSheetsProfileRecords.Read(db,profile.BusinessId,store.InstanceId)!=profile||
            db.Scalar("SELECT Id FROM Owners WHERE Id=$owner AND BusinessId=$business AND Active=1;",("$owner",profile.ConfirmedBy),("$business",profile.BusinessId)) is null)
            throw new GoogleConnectionFailure("GoogleSheetsSchedulePermissionUnavailable","Automatic readable export settings changed or require active-owner confirmation. Review the schedule.");
    }
}
