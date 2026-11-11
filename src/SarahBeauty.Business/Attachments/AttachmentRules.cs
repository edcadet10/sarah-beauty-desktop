using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Attachments;

internal static class AttachmentRules
{
    internal static AttachmentImport Normalize(AttachmentImport draft)
    {
        draft=draft with{Name=draft.Name.Trim(),Category=draft.Category.Trim(),Reason=draft.Reason.Trim(),PermissionUse=draft.PermissionUse.Trim()};
        if(draft.Name.Length is <1 or >200 || draft.Reason.Length is <1 or >1000 || !new[]{"Receipt","Evidence","Customer photo"}.Contains(draft.Category))
            throw new ArgumentException("Enter a file name, category (Receipt, Evidence, or Customer photo), and an import/version reason.");
        if(draft.AttachmentId.Length==0 && draft.ExpectedVersion!=0 || draft.AttachmentId.Length>0 && (draft.ExpectedVersion<1 || !Guid.TryParseExact(draft.AttachmentId,"D",out _)))
            throw new ArgumentException("Choose the current attachment version before adding a replacement.");
        if(draft.Category!="Customer photo" && (draft.CustomerId.Length>0 || draft.PermissionUse.Length>0))throw new ArgumentException("Customer photo permission context belongs only to a Customer photo attachment.");
        return draft;
    }

    internal static string Permission(DbWork db,OwnerSession owner,AttachmentImport draft)
    {
        if(draft.Category!="Customer photo")return "";
        var customer=CustomerBook.Find(db,owner,draft.CustomerId)??throw new ArgumentException("Choose the customer whose photo permission covers this image.");
        if(customer.Archived || customer.PhotoPermission!="Granted" || draft.PermissionUse.Length==0 || !string.Equals(customer.PermissionScope.Trim(),draft.PermissionUse,StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A current Granted photo permission with the exact recorded use/scope is required. Review the customer's permission first.");
        return db.Scalar("SELECT Id FROM CustomerPermissions WHERE CustomerId=$id AND BusinessId=$business ORDER BY rowid DESC LIMIT 1;",
            ("$id",customer.Id),("$business",owner.BusinessId)) as string??throw new InvalidDataException("The customer's permission history is missing.");
    }

    internal static void MayRead(DbWork db,OwnerSession owner,AttachmentVersion version)
    {
        if(version.Category=="Customer photo")_ = Permission(db,owner,new(version.Name,version.Category,version.Reason,version.CustomerId,version.PermissionUse));
    }

    internal static void Replacement(AttachmentImport draft,AttachmentVersion? previous)
    {
        if(previous is null){if(draft.AttachmentId.Length>0)throw new ArgumentException("The attachment could not be found in this business.");return;}
        if(previous.Number!=draft.ExpectedVersion)throw new InvalidOperationException("This attachment has a newer version. Reload its history before importing a replacement.");
        if(previous.Category!=draft.Category || previous.CustomerId!=draft.CustomerId || previous.PermissionUse!=draft.PermissionUse)
            throw new ArgumentException("A replacement must retain the attachment's category, customer, and permission use. Import a separate attachment for a different purpose.");
    }
}
