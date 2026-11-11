using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Finance;

public sealed class FinancialEvidence(BusinessStore store)
{
    public StoredAttachment Read(OwnerSession owner,ManagedEvidenceFile evidence)=>store.ReadSnapshot(db=>{store.RequireSession(db,owner);return Check(db,owner,evidence);});
    internal static void Validate(DbWork db,BusinessStore store,OwnerSession owner,ManagedEvidenceFile? evidence,bool verifyContents)
    {
        if(evidence is null)return;var file=Check(db,owner,evidence);
        if(verifyContents){using var verified=new ManagedAttachments(store).OpenVerified(file.Version);}
    }
    private static StoredAttachment Check(DbWork db,OwnerSession owner,ManagedEvidenceFile evidence)
    {
        var file=AttachmentQueries.Get(db,owner,evidence.VersionId);
        if(file.Version.Category=="Customer photo")throw new ArgumentException("A private customer photo cannot be used as financial evidence. Choose a receipt or evidence document.");
        if(ManagedEvidenceFile.From(file)!=evidence)throw new InvalidDataException("The financial evidence reference does not match its reviewed file version. Review the original managed file before saving or approving.");
        return file;
    }
}
