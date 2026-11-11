using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

public sealed record BackupScheduleDraft(bool Enabled,int IntervalMinutes,string Directory,int ExpectedRevision=0);
public sealed record BackupSchedule(int Version,string Id,string BusinessId,string InstanceId,int Revision,bool Enabled,int IntervalMinutes,string Directory,string ConfirmedBy,string ConfirmedAtUtc);
internal sealed record BackupPermit(BackupSchedule Schedule,string Recipient,string RecipientFingerprint);
public sealed class BackupSchedules(BusinessStore store)
{
    internal static string Key(string business,string instance)=>"LocalBackupSchedule/"+business+"/"+instance;
    public BackupSchedule? Current(OwnerSession owner)=>store.Read(db=>{store.RequireSession(db,owner);return Read(db,owner.BusinessId);});
    public BackupSchedule Save(OwnerSession owner,BackupScheduleDraft draft)
    {
        if(draft.IntervalMinutes is <1 or >10080)throw new ArgumentException("Choose a backup interval from 1 minute to 7 days.");
        string directory=string.IsNullOrWhiteSpace(draft.Directory)?"":Path.TrimEndingDirectorySeparator(Path.GetFullPath(draft.Directory));
        if(draft.Enabled){if(!System.IO.Directory.Exists(directory))throw new ArgumentException("Choose an existing private archive folder.");ArchivePaths.RejectLink(directory);}
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);var prior=Read(db,owner.BusinessId);if((prior?.Revision??0)!=draft.ExpectedRevision)throw new ArgumentException("The backup schedule changed. Reload it before saving.");
            if(draft.Enabled && RecoveryProfiles.Read(db,owner.BusinessId) is null)throw new ArgumentException("Save and confirm portable recovery material before enabling automatic archives.");
            if(prior is not null && prior.Enabled==draft.Enabled && prior.IntervalMinutes==draft.IntervalMinutes && prior.Directory==directory)return prior;
            var saved=new BackupSchedule(1,prior?.Id??Guid.NewGuid().ToString(),owner.BusinessId,store.InstanceId,checked((prior?.Revision??0)+1),draft.Enabled,draft.IntervalMinutes,directory,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));
            db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$json) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value;",("$key",Key(owner.BusinessId,store.InstanceId)),("$json",JsonSerializer.Serialize(saved)));
            BusinessStore.Audit(db,owner,saved.Enabled?"Enabled or changed automatic local archives":"Disabled automatic local archives",saved.Id);return saved;
        });
    }
    internal BackupPermit Authorize()=>store.Read(db=>
    {
        string business=db.Read("SELECT Id FROM Business;",r=>r.GetString(0)).Single();var schedule=Read(db,business)??throw new UnauthorizedAccessException("Automatic local archives are not configured.");
        var profile=RecoveryProfiles.Read(db,business)??throw new UnauthorizedAccessException("Portable recovery material is not confirmed.");var permit=new BackupPermit(schedule,profile.Recipient,profile.RecipientFingerprint);Require(db,permit);return permit;
    });
    internal void Require(DbWork db,BackupPermit permit)
    {
        var actual=Read(db,permit.Schedule.BusinessId);var profile=RecoveryProfiles.Read(db,permit.Schedule.BusinessId);
        if(actual is null || actual!=permit.Schedule || !actual.Enabled || actual.InstanceId!=store.InstanceId || profile is null || profile.Recipient!=permit.Recipient || profile.RecipientFingerprint!=permit.RecipientFingerprint || db.Scalar("SELECT Id FROM Owners WHERE Id=$id AND BusinessId=$business AND Active=1;",("$id",actual.ConfirmedBy),("$business",actual.BusinessId)) is null)
            throw new UnauthorizedAccessException("The automatic archive permission changed. Review the current recovery settings.");
    }
    private BackupSchedule? Read(DbWork db,string business)
    {
        string? json=db.Scalar("SELECT Value FROM AppMetadata WHERE Key=$key;",("$key",Key(business,store.InstanceId))) as string;if(json is null)return null;
        var value=JsonSerializer.Deserialize<BackupSchedule>(json)??throw new InvalidDataException("The local archive schedule is incomplete.");
        if(value.Version!=1 || value.BusinessId!=business || value.InstanceId!=store.InstanceId || value.Revision<1 || value.IntervalMinutes is <1 or >10080 || !Guid.TryParse(value.Id,out _))throw new InvalidDataException("The local archive schedule needs review.");return value;
    }
}
