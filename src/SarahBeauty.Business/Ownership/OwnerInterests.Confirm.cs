using System.Text.Json;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Ownership;

public sealed partial class OwnerInterests
{
    internal Action? BeforeConfirmationCommit {get;set;}
    public OwnerInterestVersion Confirm(OwnerSession owner,OwnerInterestDraft draft,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();draft=OwnerInterestRules.Normalize(draft);
        return store.Transaction(db=>
        {
            cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);
            var history=OwnerInterestQueries.Read(db,owner.BusinessId);RequireRevision(history,draft);
            var shares=ConfirmShares(OwnerInterestQueries.Owners(db,owner.BusinessId),draft);var evidence=Evidence(db,owner,draft.EvidenceVersionId);
            using var verified=evidence is null?null:new ManagedAttachments(store).OpenVerified(evidence.Version);
            var value=new OwnerInterestVersion(1,Guid.NewGuid().ToString(),owner.BusinessId,checked(draft.ExpectedRevision+1),draft.EffectiveFrom,draft.SupersedesId,
                shares,draft.Reason,evidence?.Version.Id,evidence?.SnapshotHash,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"));
            string json=JsonSerializer.Serialize(value),hash=OwnerInterestCodec.Hash(json);value=OwnerInterestCodec.Read(json,hash);
            OwnerInterestWrites.Insert(db,value,json,hash);BusinessStore.Audit(db,owner,"Confirmed dated ownership percentages",value.Id);
            BeforeConfirmationCommit?.Invoke();cancellation.ThrowIfCancellationRequested();store.RequireSession(db,owner);return value;
        });
    }
    private static void RequireRevision(IReadOnlyList<OwnerInterestVersion> history,OwnerInterestDraft draft)
    {
        if((history.LastOrDefault()?.Revision??0)!=draft.ExpectedRevision)throw new InvalidOperationException("Ownership settings changed. Reload and review their current revision before confirming.");
        if(draft.SupersedesId is null)
        {
            if(history.Any(v=>v.EffectiveFrom==draft.EffectiveFrom))throw new ArgumentException("That effective date already has a confirmation. Choose its latest version to record a correction.");
            return;
        }
        var prior=history.SingleOrDefault(v=>v.Id==draft.SupersedesId);
        if(prior is null || prior.EffectiveFrom!=draft.EffectiveFrom || history.Any(v=>v.SupersedesId==prior.Id))
            throw new ArgumentException("Correct the latest confirmation for the same effective date. Reload its history before continuing.");
    }
    private static IReadOnlyList<ConfirmedOwnerShare> ConfirmShares(IReadOnlyList<OwnerInterestOwner> owners,OwnerInterestDraft draft)
    {
        if(owners.Count!=draft.Shares.Count || !owners.Select(o=>o.Id).ToHashSet(StringComparer.Ordinal).SetEquals(draft.Shares.Select(s=>s.OwnerId)))
            throw new ArgumentException("Include every owner of this business, including owners whose login is disabled. Record an explicit zero when confirmed.");
        var names=owners.ToDictionary(o=>o.Id,o=>o.Name,StringComparer.Ordinal);
        return Array.AsReadOnly(draft.Shares.Select(s=>new ConfirmedOwnerShare(s.OwnerId,names[s.OwnerId],s.Fraction!.Value)).ToArray());
    }
    private static StoredAttachment? Evidence(DbWork db,OwnerSession owner,string? id)
    {
        if(id is null)return null;var evidence=AttachmentQueries.Get(db,owner,id);
        if(evidence.Version.Category=="Customer photo")throw new ArgumentException("Choose a receipt or evidence file for ownership confirmation. Customer photos have a separate permission scope.");
        return evidence;
    }
}
