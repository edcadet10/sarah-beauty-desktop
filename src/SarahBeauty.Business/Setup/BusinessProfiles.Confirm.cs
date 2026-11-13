using System.Text.Json;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Setup;

public sealed partial class BusinessProfiles
{
    internal Action? BeforeProfileCommit {get;set;}
    public BusinessProfileVersion Confirm(OwnerSession owner,BusinessProfileChange draft,string phrase,string requestId,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();draft=BusinessProfileRules.NormalizeChange(draft);
        if(!Guid.TryParseExact(requestId,"D",out var request)||request==Guid.Empty)throw new ArgumentException("Use a permanent request ID for this profile review.");
        store.Read(db=>{store.RequireSession(db,owner);return true;});new OwnerAccess(store).Confirm(owner,phrase);cancellation.ThrowIfCancellationRequested();
        return store.Transaction(db=>Confirm(db,owner,draft,request.ToString(),cancellation));
    }
    private BusinessProfileVersion Confirm(DbWork db,OwnerSession owner,BusinessProfileChange draft,string request,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);var history=BusinessProfileQueries.Read(db,owner.BusinessId);string payload=BusinessProfileCodec.Payload(draft);var prior=history.SingleOrDefault(v=>v.RequestId==request);
        if(prior is not null)
        {
            if(BusinessProfileCodec.Payload(BusinessProfileCodec.Change(prior))!=payload)throw new ArgumentException("This request already recorded a different profile review. Review its history before continuing.");
            cancellation.ThrowIfCancellationRequested();return prior;
        }
        if(draft.ExpectedRevision!=(history.LastOrDefault()?.Revision??0))throw new ArgumentException("The private business profile changed. Reload its current revision before confirming.");
        foreach(var review in new[]{draft.Profile.Formation,draft.Profile.EinReview,draft.Profile.Banking,draft.Profile.Tax}){cancellation.ThrowIfCancellationRequested();FinancialEvidence.Validate(db,store,owner,review.Evidence,verifyContents:true);}
        var value=new BusinessProfileVersion(1,Guid.NewGuid().ToString(),owner.BusinessId,checked(draft.ExpectedRevision+1),request,(string)db.Scalar("SELECT Value FROM AppMetadata WHERE Key='StoreMode';")!,draft.Profile,draft.Reason,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));
        Insert(db,value,payload);db.Execute("UPDATE Business SET DisplayName=$name WHERE Id=$id;",("$name",value.Profile.DisplayName),("$id",owner.BusinessId));BusinessStore.Audit(db,owner,"Confirm private business profile",value.Id);
        BeforeProfileCommit?.Invoke();cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);return BusinessProfileQueries.Read(db,owner.BusinessId).Single(v=>v.Id==value.Id);
    }
    private static void Insert(DbWork db,BusinessProfileVersion value,string payload)
    {
        string json=JsonSerializer.Serialize(value);db.Execute("""
            INSERT INTO BusinessProfileVersions(Id,BusinessId,Revision,RequestId,SnapshotJson,SnapshotHash,PayloadHash,RecordedByOwnerId,RecordedAtUtc)
            VALUES($id,$business,$revision,$request,$json,$hash,$payload,$owner,$at);
            """,("$id",value.Id),("$business",value.BusinessId),("$revision",value.Revision),("$request",value.RequestId),("$json",json),("$hash",BusinessProfileCodec.Hash(json)),("$payload",payload),("$owner",value.RecordedByOwnerId),("$at",value.RecordedAtUtc));
    }
}
