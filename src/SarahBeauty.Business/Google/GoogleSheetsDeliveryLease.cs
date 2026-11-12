using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Google;

internal static class GoogleSheetsDeliveryLease
{
    internal static FileStream? TryAcquire(BusinessStore store)
    {
        return Acquire(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(store.DatabasePath))!,"google-sheets-delivery.lock"));
    }
    internal static FileStream? TryQueue(BusinessStore store)=>Acquire(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(store.DatabasePath))!,"google-sheets-queue.lock"));
    private static FileStream? Acquire(string path)
    {
        ArchivePaths.RejectLink(path);
        try{return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
        catch(IOException error) when((error.HResult&0xffff) is 32 or 33){return null;}
    }
}
