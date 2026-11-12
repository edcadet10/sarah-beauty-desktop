using System.Security.Cryptography;
using System.Text.Json;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

internal static class ReadyStoreChecks
{
    internal static RecoveredStoreReceipt Read(DbWork db,string request)
    {
        string json=db.Scalar("SELECT Value FROM AppMetadata WHERE Key=$key;",("$key","RecoveredStore/"+Guid.Parse(request).ToString())) as string??throw new InvalidDataException("The recovered-copy provenance is unavailable.");
        var receipt=JsonSerializer.Deserialize<RecoveredStoreReceipt>(json)??throw new InvalidDataException("The recovered-copy provenance is incomplete.");
        if(receipt.Version!=2 || receipt.SourceManifest is null || receipt.RequestId!=Guid.Parse(request).ToString() || !receipt.PreviousPrimaryAcknowledged)throw new InvalidDataException("Prepare a new recovered copy with the current app before activation. Existing copies are preserved.");return receipt;
    }
    internal static void Verify(DbWork db,string folder,RecoveredStoreReceipt receipt,CancellationToken cancellation)
    {
        var m=ArchiveManifestChecks.Read(JsonSerializer.SerializeToUtf8Bytes(receipt.SourceManifest));StoreInstanceGuard.Require(db,receipt.StoreId,receipt.InstanceId,"Prepared");
        if(m.StoreId!=receipt.StoreId || m.Mode!="TEST" || m.Watermark!=receipt.SourceWatermark || (string?)db.Scalar("SELECT DisplayName FROM Business WHERE Id=$id;",("$id",m.BusinessId))!=m.BusinessName || (long?)db.Scalar("SELECT Watermark FROM Business WHERE Id=$id;",("$id",m.BusinessId))!=m.Watermark+1)throw new InvalidDataException("The prepared business changed before activation.");
        var facts=ArchiveDatabaseChecks.Read(db);if(facts.MovementCount!=m.Facts.MovementCount || facts.JournalLineCount!=m.Facts.JournalLineCount || facts.TotalDebits!=m.Facts.TotalDebits || facts.TotalCredits!=m.Facts.TotalCredits)throw new InvalidDataException("The prepared financial facts changed before activation.");
        RecoveredTableInventory.Verify(facts,m);
        var files=m.Files.Where(f=>f.Kind!="Database").ToDictionary(f=>f.RelativePath,StringComparer.Ordinal);var references=ArchiveReferences.Read(db,m.BusinessId);
        if(files.Count!=references.Length)throw new InvalidDataException("The prepared managed-file references changed before activation.");
        foreach(var reference in references)
        {
            cancellation.ThrowIfCancellationRequested();if(!files.TryGetValue(ArchivePaths.Normalize(reference.RelativePath),out var file)||file.Sha256!=reference.ExpectedHash||file.Kind!=reference.Kind)throw new InvalidDataException("A prepared file reference changed before activation.");
            using var input=new FileStream(new ArchivePaths(folder).Resolve(file.RelativePath),FileMode.Open,FileAccess.Read,FileShare.Read);
            if(input.Length!=file.ByteLength || Convert.ToHexString(SHA256.HashData(input))!=file.Sha256)throw new InvalidDataException("A prepared file changed before activation.");
        }
    }
}
